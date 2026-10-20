import test from 'node:test';
import assert from 'node:assert/strict';
import { appError, PROXY_ORIGIN, startServer, type TestServer } from './server.ts';
// 画面からサーバーへのtRPC / HTTPとSSEの公開境界へ、UIからは再現できない入力を送る。
async function withServer(
  run: (server: TestServer) => Promise<void>,
  authMode: 'demo' | 'proxy' = 'demo',
) {
  const server = await startServer(authMode);
  try {
    await run(server);
  } finally {
    await server.close();
  }
}
const save = '/api/trpc/notes.save';
const remove = '/api/trpc/notes.remove';
await test('同一originの保存を受け入れ、Originなし・他originの変更要求をDBを変えずに拒否する', () =>
  withServer(async (server) => {
    const cookie = await server.signIn('alice');
    const before = server.rows();
    for (const origin of [undefined, 'http://evil.example.test', 'null']) {
      const reply = await server.send(save, {
        body: { title: '拒否される', body: '' },
        cookie,
        headers: { origin },
      });
      assert.equal(reply.status, 403, `Origin=${origin}`);
      assert.deepEqual(reply.json(), { message: '同一サイトから操作してください。' });
    }
    assert.deepEqual(server.rows(), before);
    const accepted = await server.send(save, { body: { title: '受け入れる', body: '' }, cookie });
    assert.equal(accepted.status, 200);
    assert.deepEqual(
      server.rows().notes.map((n) => [n.owner_id, n.title]),
      [['alice', '受け入れる']],
    );
  }));
await test('許可していない接続先名への要求をDBを変えずに拒否する', () =>
  withServer(async (server) => {
    const cookie = await server.signIn('alice');
    const before = server.rows();
    for (const path of ['/api/session', '/events/notes']) {
      const reply = await server.send(path, { cookie, headers: { host: 'evil.example.test' } });
      assert.equal(reply.status, 403, path);
      assert.deepEqual(reply.json(), { message: '接続先が不正です。' });
    }
    const reply = await server.send(save, {
      body: { title: '拒否される', body: '' },
      cookie,
      headers: { host: 'evil.example.test' },
    });
    assert.equal(reply.status, 403);
    assert.deepEqual(server.rows(), before);
  }));
await test('未認証のtRPCとSSEの購読を拒否する', () =>
  withServer(async (server) => {
    const list = await server.send('/api/trpc/notes.list');
    assert.equal(list.status, 401);
    assert.deepEqual(appError(list), {
      code: 'UNAUTHENTICATED',
      message: '利用者を確認できません。',
    });
    const created = await server.send(save, { body: { title: '未認証', body: '' } });
    assert.equal(created.status, 401);
    assert.deepEqual(appError(created), {
      code: 'UNAUTHENTICATED',
      message: '利用者を確認できません。',
    });
    const forged = await server.send('/events/notes', { cookie: 'aidd_session=forged' });
    assert.equal(forged.status, 401);
    assert.deepEqual(forged.json(), { message: '利用者を確認できません。' });
    assert.deepEqual(server.rows().notes, []);
  }));
await test('契約の形に合わない入力を項目別の公開エラーで拒否し、DBを変えない', () =>
  withServer(async (server) => {
    const cookie = await server.signIn('alice');
    const before = server.rows();
    const cases: Array<[string, unknown, Record<string, string>]> = [
      [save, { title: 'a', body: '', owner: 'bob' }, { form: 'Unrecognized key: "owner"' }],
      [save, { title: 1, body: '' }, { title: 'Invalid input: expected string, received number' }],
      [save, { id: 'not-uuid', version: 1, title: 'a', body: '' }, { id: 'Invalid UUID' }],
      [
        remove,
        { id: crypto.randomUUID(), version: 0 },
        { version: 'Too small: expected number to be >0' },
      ],
    ];
    for (const [path, body, fieldErrors] of cases) {
      const reply = await server.send(path, { body, cookie });
      assert.equal(reply.status, 400, JSON.stringify(body));
      assert.deepEqual(appError(reply), {
        code: 'VALIDATION',
        message: '入力の形式を確認してください。',
        fieldErrors,
      });
    }
    // JSONとして読めない本文は解析の詳細を返さない。
    const malformed = await server.send(save, { rawBody: '{"title":', cookie });
    assert.equal(malformed.status, 400);
    assert.equal(
      (appError(malformed) as { message: string }).message,
      '処理を完了できませんでした。',
    );
    assert.deepEqual(server.rows(), before);
  }));
await test('1MBを超える要求本文をDBを変えずに拒否する', () =>
  withServer(async (server) => {
    const cookie = await server.signIn('alice');
    const before = server.rows();
    const reply = await server.send(save, {
      body: { title: '大きすぎる', body: 'a'.repeat(1024 * 1024) },
      cookie,
    });
    assert.equal(reply.status, 413);
    assert.deepEqual(server.rows(), before);
  }));
await test('他者のメモの参照・更新・削除を拒否し、DBを変えない', () =>
  withServer(async (server) => {
    const alice = await server.signIn('alice');
    const saved = await server.send(save, {
      body: { title: '所有者別', body: 'private' },
      cookie: alice,
    });
    const note = (saved.json() as { result: { data: { id: string; version: number } } }).result
      .data;
    const bob = await server.signIn('bob');
    const before = server.rows();
    const replies = [
      await server.send(
        `/api/trpc/notes.get?input=${encodeURIComponent(JSON.stringify({ id: note.id }))}`,
        {
          cookie: bob,
        },
      ),
      await server.send(save, {
        body: { id: note.id, version: note.version, title: '所有者別', body: 'attack' },
        cookie: bob,
      }),
      await server.send(remove, { body: { id: note.id, version: note.version }, cookie: bob }),
    ];
    for (const reply of replies) {
      assert.equal(reply.status, 404);
      assert.deepEqual(appError(reply), {
        code: 'NOT_FOUND',
        message: '対象のメモが見つかりません。',
      });
    }
    const list = await server.send('/api/trpc/notes.list', { cookie: bob });
    assert.deepEqual((list.json() as { result: { data: unknown[] } }).result.data, []);
    assert.deepEqual(server.rows(), before);
    assert.deepEqual(
      server.rows().notes.map((n) => [n.owner_id, n.body, n.version]),
      [['alice', 'private', 1]],
    );
  }));
await test('参照用ユーザー以外のサインインを拒否し、利用者を作らない', () =>
  withServer(async (server) => {
    const before = server.rows();
    for (const body of [{ user: 'carol' }, { user: 'constructor' }, { user: 1 }, {}]) {
      const reply = await server.send('/api/demo/sign-in', { body });
      assert.equal(reply.status, 400, JSON.stringify(body));
      assert.deepEqual(reply.json(), { message: '参照用ユーザーが不正です。' });
      assert.equal(reply.headers['set-cookie'], undefined);
    }
    assert.deepEqual(server.rows(), before);
  }));
await test('proxy構成では形式に合う認証済み利用者ヘッダーだけを受け入れる', () =>
  withServer(async (server) => {
    const host = new URL(PROXY_ORIGIN).host;
    for (const user of [undefined, '', 'a b', 'x'.repeat(129), '<script>']) {
      const headers = { host, 'x-authenticated-user': user };
      const session = await server.send('/api/session', { headers });
      assert.deepEqual(session.json(), { user: null, mode: 'proxy' }, `user=${user}`);
      const created = await server.send(save, { body: { title: '拒否', body: '' }, headers });
      assert.equal(created.status, 401, `user=${user}`);
      const events = await server.send('/events/notes', { headers });
      assert.equal(events.status, 401, `user=${user}`);
    }
    assert.deepEqual(server.rows(), { users: [], notes: [] });
    const headers = { host, 'x-authenticated-user': 'carol@example.test' };
    const created = await server.send(save, { body: { title: '受け入れる', body: '' }, headers });
    assert.equal(created.status, 200);
    const signIn = await server.send('/api/demo/sign-in', { body: { user: 'alice' }, headers });
    assert.equal(signIn.status, 404);
    assert.deepEqual(
      server.rows().notes.map((n) => [n.owner_id, n.title]),
      [['carol@example.test', '受け入れる']],
    );
  }, 'proxy'));
await test('本番のHTTP処理にDB初期化APIを公開しない', () =>
  withServer(async (server) => {
    const cookie = await server.signIn('alice');
    const reply = await server.send('/api/test/reset', { body: {}, cookie });
    assert.equal(reply.status, 404);
  }));
