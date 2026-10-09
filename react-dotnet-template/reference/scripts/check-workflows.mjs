import { readdir } from 'node:fs/promises';
import { basename, resolve } from 'node:path';
import { root, run } from './lib.mjs';

const directory = resolve(root, basename(root) === 'reference' ? '..' : '.', '.github/workflows');
const workflows = (await readdir(directory))
  .filter((name) => /\.ya?ml$/.test(name))
  .map((name) => resolve(directory, name));
await run('mise', [
  'exec',
  'actionlint',
  '--',
  'actionlint',
  '-shellcheck=',
  '-pyflakes=',
  ...workflows,
]);
