import { spawnSync } from 'node:child_process';
import { readdirSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const workflows = ['.', 'wails-template', 'react-template', 'react-dotnet-template']
  .flatMap(directory => {
    const path = resolve(root, directory, '.github/workflows');
    return readdirSync(path).filter(name => /\.ya?ml$/.test(name)).map(name => resolve(path, name));
  });
const result = spawnSync('actionlint', ['-shellcheck=', '-pyflakes=', ...workflows], {
  cwd: root,
  stdio: 'inherit',
});
if (result.error) throw result.error;
process.exit(result.status ?? 1);
