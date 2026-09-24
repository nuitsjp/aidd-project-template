import { existsSync, copyFileSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { npm, root } from './lib.mjs';
const expectedNode = readFileSync(join(root, '.nvmrc'), 'utf8').trim();
if (process.versions.node !== expectedNode)
    throw new Error(`Node.js ${expectedNode}が必要です（実行中: ${process.versions.node}）。`);
await npm('ci');
if (!existsSync(join(root, '.env')))
    copyFileSync(join(root, '.env.example'), join(root, '.env'));
await npm('run', 'routes');
console.log('起動: npm run dev ／ ブラウザ導入: npx playwright install --only-shell chromium');
