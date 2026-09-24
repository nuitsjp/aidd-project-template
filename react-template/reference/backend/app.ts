import Fastify from 'fastify';
import cookie from '@fastify/cookie';
import staticFiles from '@fastify/static';
import { fastifyTRPCPlugin, type CreateFastifyContextOptions, type FastifyTRPCPluginOptions, } from '@trpc/server/adapters/fastify';
import { openDatabase } from './db/database.ts';
import { NotesService } from './features/notes/service.ts';
import { configureIdentity } from './http/auth.ts';
import { appRouter, type AppRouter, type Context } from './http/router.ts';
import type { AppConfig } from './config.ts';
// DB・購読・セッションはインスタンス所有。モジュール単位のDB singletonを作らない。
export async function createApp(config: AppConfig) {
    const app = Fastify({
        bodyLimit: 1024 * 1024,
        logger: {
            level: config.logLevel,
            redact: ['req.headers.authorization', 'req.headers.cookie', 'res.headers["set-cookie"]'],
            serializers: {
                req: (req) => ({ method: req.method, url: String(req.url).split('?')[0] }),
            },
        },
    });
    const db = openDatabase(config.databasePath);
    const notes = new NotesService(db, error => app.log.warn({ err: error }, '変更通知に失敗しました'));
    const closeStreams = new Set<() => void>();
    app.addHook('preClose', async () => {
        for (const close of closeStreams)
            close();
        closeStreams.clear();
    });
    app.addHook('onClose', async () => { db.close(); });
    try {
        await app.register(cookie);
        app.addHook('onRequest', async (req, reply) => {
            if (!(req.url.startsWith('/api/') || req.url === '/events/notes'))
                return;
            const host = req.headers.host ?? '';
            const bound = app.server.address();
            const port = bound && typeof bound === 'object' ? bound.port : config.port;
            const localHost = config.host === '::1' ? `[::1]:${port}` : `${config.host}:${port}`;
            const expected = config.publicOrigin ?? `http://${localHost}`;
            if (host !== localHost && host !== `localhost:${port}` && host !== new URL(expected).host) {
                return reply.code(403).send({ message: '接続先が不正です。' });
            }
            const origin = req.headers.origin;
            const allowed = [expected, ...config.allowedOrigins];
            if ((origin && !allowed.includes(origin)) || (req.method === 'POST' && !origin)) {
                return reply.code(403).send({ message: '同一サイトから操作してください。' });
            }
            reply.header('Cache-Control', 'no-store');
        });
        app.addHook('onSend', async (_req, reply, payload) => {
            reply.header('X-Content-Type-Options', 'nosniff').header('Referrer-Policy', 'same-origin');
            reply.header('Content-Security-Policy', "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; connect-src 'self'; img-src 'self' data:; font-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'");
            return payload;
        });
        const resolveUser = configureIdentity(app, db, config);
        await app.register(fastifyTRPCPlugin, {
            prefix: '/api/trpc',
            trpcOptions: {
                router: appRouter,
                createContext: ({ req }: CreateFastifyContextOptions): Context => ({ user: resolveUser(req), notes }),
                onError: ({ error, path }) => {
                    if (error.code === 'INTERNAL_SERVER_ERROR') {
                        app.log.error({ err: error.cause, operation: path }, '機能操作に失敗しました');
                    }
                },
            } satisfies FastifyTRPCPluginOptions<AppRouter>['trpcOptions'],
        });
        app.get('/events/notes', async (req, reply) => {
            const user = resolveUser(req);
            if (!user)
                return reply.code(401).send({ message: '利用者を確認できません。' });
            reply.hijack();
            reply.raw.writeHead(200, {
                'Content-Type': 'text/event-stream', 'Cache-Control': 'no-store',
                'Connection': 'keep-alive', 'X-Accel-Buffering': 'no',
                'X-Content-Type-Options': 'nosniff',
            });
            const send = (event: string) => {
                if (!reply.raw.destroyed)
                    reply.raw.write(`event: ${event}\ndata: {}\n\n`);
            };
            const off = notes.subscribe(user.id, () => {
                try {
                    send('notes.changed');
                }
                catch (error) {
                    app.log.warn({ err: error }, '通知を送れませんでした');
                }
            });
            const timer = setInterval(() => {
                if (!reply.raw.destroyed)
                    reply.raw.write(': keepalive\n\n');
            }, 15000);
            timer.unref();
            let closed = false;
            const close = () => {
                if (closed)
                    return;
                closed = true;
                clearInterval(timer);
                off();
                closeStreams.delete(close);
                reply.raw.end();
            };
            closeStreams.add(close);
            reply.raw.on('close', close);
            send('ready');
        });
        app.get('/health', async () => ({ status: 'ok' }));
        await app.register(staticFiles, { root: config.frontendDist, wildcard: false });
        app.setNotFoundHandler(async (req, reply) => {
            if (req.method === 'GET' && !req.url.startsWith('/api/') && !req.url.startsWith('/events/') && req.headers.accept?.includes('text/html')) {
                reply.header('Cache-Control', 'no-store');
                return reply.sendFile('index.html');
            }
            return reply.code(404).send({ message: '見つかりません。' });
        });
        await app.ready();
        return app;
    }
    catch (error) {
        await app.close();
        throw error;
    }
}
