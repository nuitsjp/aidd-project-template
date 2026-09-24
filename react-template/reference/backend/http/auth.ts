import { randomBytes } from 'node:crypto';
import type { FastifyInstance, FastifyRequest } from 'fastify';
import type { DatabaseSync } from 'node:sqlite';
import type { AppConfig } from '../config.ts';
import type { Principal } from '../../contracts/notes.ts';
import { ensureUser } from '../features/identity/users.ts';
const COOKIE = 'aidd_session';
const DEMO_USERS: Record<string, string> = { alice: 'Alice', bob: 'Bob' };
export function configureIdentity(app: FastifyInstance, db: DatabaseSync, config: AppConfig) {
    const sessions = new Map<string, {
        user: Principal;
        expires: number;
    }>();
    function resolveUser(req: FastifyRequest): Principal | null {
        if (config.authMode === 'proxy') {
            // 信頼済み同一ホストのリバースプロキシが認証して設定する。任意クライアントのヘッダーは信頼しない。
            const id = req.headers['x-authenticated-user'];
            if (typeof id !== 'string' || !/^[a-zA-Z0-9@._:+/-]{1,128}$/.test(id))
                return null;
            return ensureUser(db, { id, name: id });
        }
        const key = req.cookies[COOKIE];
        const session = key ? sessions.get(key) : undefined;
        if (!session)
            return null;
        if (session.expires <= Date.now()) {
            sessions.delete(key!);
            return null;
        }
        return session.user;
    }
    app.get('/api/session', async (req, reply) => {
        reply.header('Cache-Control', 'no-store');
        return { user: resolveUser(req), mode: config.authMode };
    });
    if (config.authMode === 'demo') {
        // ローカル参照実装のユーザー選択であり、認証機能ではない。本番モードではルートを登録しない。
        app.post('/api/demo/sign-in', async (req, reply) => {
            const input = req.body as {
                user?: unknown;
            } | null;
            const id = typeof input?.user === 'string' ? input.user : '';
            if (!Object.hasOwn(DEMO_USERS, id))
                return reply.code(400).send({ message: '参照用ユーザーが不正です。' });
            for (const [key, session] of sessions)
                if (session.expires <= Date.now())
                    sessions.delete(key);
            const previous = req.cookies[COOKIE];
            if (previous)
                sessions.delete(previous);
            if (sessions.size >= 100)
                return reply.code(429).send({ message: '参照用セッションの上限です。' });
            const user = ensureUser(db, { id, name: DEMO_USERS[id]! });
            const token = randomBytes(32).toString('hex');
            sessions.set(token, { user, expires: Date.now() + 8 * 60 * 60 * 1000 });
            reply.setCookie(COOKIE, token, { httpOnly: true, sameSite: 'strict', secure: false, path: '/', maxAge: 8 * 60 * 60 });
            return { user };
        });
        app.post('/api/demo/sign-out', async (req, reply) => {
            const key = req.cookies[COOKIE];
            if (key)
                sessions.delete(key);
            reply.clearCookie(COOKIE, { path: '/' });
            return { ok: true };
        });
    }
    return resolveUser;
}
