import { npm, run } from './lib.mjs';
await npm('run', 'typecheck');
await npm('run', 'lint');
await run(process.env.PYTHON ?? 'python', ['scripts/doc_check.py', '.']);
await npm('run', 'test:core');
await npm('run', 'test:unit');
await npm('run', 'build');
await npm('exec', '--', 'playwright', 'test');
