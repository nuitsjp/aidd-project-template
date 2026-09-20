import { copyFileSync, cpSync, mkdirSync } from 'node:fs';
import { dirname, isAbsolute, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');

try {
  const kind = process.argv[2];
  if (process.argv.length !== 4 || !['wails', 'react'].includes(kind)) {
    throw new Error('使い方: mise run init:wails|init:react <新しい出力先>');
  }
  const destination = resolve(process.argv[3]);
  const sources = ['template', `${kind}-template`].map(name => resolve(root, name));
  for (const source of ['template', 'wails-template', 'react-template'].map(name => resolve(root, name))) {
    const path = relative(source, destination);
    if (path === '' || (!isAbsolute(path) && path !== '..' && !path.startsWith(`..${sep}`))) {
      throw new Error('出力先はtemplate/・wails-template/・react-template/の外に指定してください。');
    }
  }
  // mkdir fails for an existing destination, before any files are copied.
  mkdirSync(destination);
  for (const source of sources) cpSync(source, destination, { recursive: true });
  copyFileSync(resolve(root, 'LICENSE'), resolve(destination, 'LICENSE'));
  console.log(`${kind}の初期状態を生成しました: ${destination}`);
} catch (error) {
  console.error(error.code === 'EEXIST' ? '出力先が既に存在します。新しい名前を指定してください。' : error.message);
  process.exitCode = 1;
}
