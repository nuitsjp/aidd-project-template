import { basename } from 'node:path';
import { npm, root, run } from './lib.mjs';
await npm('run', 'typecheck');
await npm('run', 'lint');
// 生成先の reference/ は親ディレクトリの共通検査器を使う。
await run(process.env.PYTHON ?? 'python', [basename(root) === 'reference' ? '../scripts/doc_check.py' : 'scripts/doc_check.py', '.']);
await npm('run', 'test:core');
await npm('run', 'test:unit');
await npm('run', 'build');
await npm('exec', '--', 'playwright', 'test');
