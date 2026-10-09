import { readdirSync } from 'node:fs';
import { basename, join } from 'node:path';
import { root, run } from './lib.mjs';

const directory = join(
  root,
  basename(root) === 'reference' ? '../.github/workflows' : '.github/workflows',
);
const workflows = readdirSync(directory)
  .filter((name) => /\.ya?ml$/.test(name))
  .map((name) => join(directory, name));
await run('mise', [
  'exec',
  'actionlint',
  '--',
  'actionlint',
  '-shellcheck=',
  '-pyflakes=',
  ...workflows,
]);
