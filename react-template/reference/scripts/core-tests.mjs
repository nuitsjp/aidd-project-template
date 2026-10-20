import { readdirSync } from 'node:fs';
import { join } from 'node:path';
import { run, root } from './lib.mjs';
// 機能・DBの直下と、公開境界のhttp/を含めて実行する。
const files = readdirSync(join(root, 'tests/core'), { recursive: true })
  .filter((f) => f.endsWith('.test.ts'))
  .map((f) => join(root, 'tests/core', f));
await run(process.execPath, [
  '--experimental-strip-types',
  '--test',
  '--test-concurrency=4',
  ...files,
]);
