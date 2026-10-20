import { mkdirSync, mkdtempSync, rmSync } from 'node:fs';
import { request } from 'node:http';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { DatabaseSync } from 'node:sqlite';
import { createApp } from '../../../backend/app.ts';
import type { AppConfig } from '../../../backend/config.ts';
export interface Reply {
  status: number;
  headers: Record<string, string | string[] | undefined>;
  json: () => unknown;
  text: string;
}
export interface RequestOptions {
  body?: unknown;
  rawBody?: string;
  headers?: Record<string, string | undefined>;
  cookie?: string;
}
export const PROXY_ORIGIN = 'https://notes.example.test';
// 本番と同じcreateAppを空きポートで待ち受け、実HTTPで公開境界へ入力する。DBはテストごとの一時ファイル。
export async function startServer(authMode: AppConfig['authMode'] = 'demo') {
  const dir = mkdtempSync(join(tmpdir(), 'aidd-http-'));
  const databasePath = join(dir, 'app.sqlite');
  const frontendDist = join(dir, 'dist');
  mkdirSync(frontendDist);
  const app = await createApp({
    host: '127.0.0.1',
    port: 0,
    databasePath,
    frontendDist,
    authMode,
    publicOrigin: authMode === 'proxy' ? PROXY_ORIGIN : undefined,
    allowedOrigins: [],
    logLevel: 'silent',
  });
  await app.listen({ host: '127.0.0.1', port: 0 });
  const address = app.server.address();
  if (!address || typeof address !== 'object') throw new Error('待ち受けポートを取得できません。');
  const port = address.port;
  const host = `127.0.0.1:${port}`;
  const origin = authMode === 'proxy' ? PROXY_ORIGIN : `http://${host}`;
  // Host・Origin・Cookieを利用者側で自由に組み立てるため、fetchではなくnode:httpで送る。
  function send(path: string, options: RequestOptions = {}): Promise<Reply> {
    const payload =
      options.rawBody ?? (options.body === undefined ? undefined : JSON.stringify(options.body));
    const method = payload === undefined ? 'GET' : 'POST';
    const headers: Record<string, string> = { host };
    if (method === 'POST') headers.origin = origin;
    if (payload !== undefined) headers['content-type'] = 'application/json';
    if (options.cookie) headers.cookie = options.cookie;
    for (const [name, value] of Object.entries(options.headers ?? {})) {
      if (value === undefined) delete headers[name];
      else headers[name] = value;
    }
    return new Promise((resolveReply, reject) => {
      const req = request({ host: '127.0.0.1', port, path, method, headers }, (res) => {
        const chunks: Buffer[] = [];
        res.on('data', (chunk: Buffer) => chunks.push(chunk));
        res.on('end', () => {
          const text = Buffer.concat(chunks).toString('utf8');
          resolveReply({
            status: res.statusCode ?? 0,
            headers: res.headers,
            text,
            json: () => JSON.parse(text) as unknown,
          });
        });
        res.on('error', reject);
      });
      req.on('error', reject);
      req.end(payload);
    });
  }
  async function signIn(user: string): Promise<string> {
    const reply = await send('/api/demo/sign-in', { body: { user } });
    if (reply.status !== 200) throw new Error(`サインインに失敗しました: ${reply.status}`);
    const cookie = reply.headers['set-cookie']?.[0];
    if (!cookie) throw new Error('セッションCookieがありません。');
    return cookie.split(';')[0]!;
  }
  // 検証は別の読取専用接続。HTTPの結果を再利用してDB更新済みと判断しない。
  function rows() {
    const db = new DatabaseSync(databasePath, { readOnly: true });
    try {
      return {
        users: db.prepare('SELECT id,name FROM users ORDER BY id').all(),
        notes: db
          .prepare('SELECT id,owner_id,title,body,version FROM notes ORDER BY owner_id,title')
          .all(),
      };
    } finally {
      db.close();
    }
  }
  return {
    origin,
    send,
    signIn,
    rows,
    close: async () => {
      try {
        await app.close();
      } finally {
        rmSync(dir, { recursive: true, force: true, maxRetries: 6, retryDelay: 100 });
      }
    },
  };
}
export type TestServer = Awaited<ReturnType<typeof startServer>>;
// tRPCの公開エラー形式から利用者向けの情報を取り出す。
export function appError(reply: Reply) {
  const body = reply.json() as { error?: { data?: { appError?: unknown } } };
  return body.error?.data?.appError;
}
