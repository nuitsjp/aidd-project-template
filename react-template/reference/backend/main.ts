import { createApp } from './app.ts';
import { readConfig } from './config.ts';
const config = readConfig(process.env);
const app = await createApp(config);
let stopping = false;
function stop(): void {
  if (stopping) return;
  stopping = true;
  const timeout = setTimeout(() => {
    app.log.error('終了待ちが期限を超えました');
    process.exit(1);
  }, 10000);
  timeout.unref();
  app.close().then(
    () => {
      clearTimeout(timeout);
      process.disconnect?.();
    },
    (error) => {
      app.log.error({ err: error }, '終了処理に失敗しました');
      process.exitCode = 1;
    },
  );
}
process.once('SIGINT', stop);
process.once('SIGTERM', stop);
// 親プロセスに管理された起動でもHTTPの管理APIを追加せず終了する。
process.on('message', (message) => {
  if (message === 'shutdown') stop();
});
process.once('disconnect', () => {
  if (!stopping) stop();
});
try {
  const address = await app.listen({ host: config.host, port: config.port });
  app.log.info({ auth: config.authMode }, 'サーバーを起動しました');
  if (config.authMode === 'demo')
    app.log.warn('ローカル参照用ユーザー選択です。認証機能ではありません。');
  process.send?.({ type: 'ready', url: address, pid: process.pid });
} catch (error) {
  app.log.error({ err: error }, '起動に失敗しました');
  await app.close();
  process.exitCode = 1;
}
