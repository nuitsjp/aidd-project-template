import { execFileSync } from 'node:child_process';
import { lstatSync, mkdirSync, readFileSync, realpathSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const files = [
  'AGENTS.md',
  'docs/standards/design-and-documentation.md',
  'docs/standards/mock-driven-development.md',
  'scripts/doc_check.py',
  '.agents/skills/usecase-docs/SKILL.md',
  '.agents/skills/usecase-docs/assets/usecase.md',
  '.agents/skills/usecase-docs/assets/scenario.md',
];
const normalize = text => text.replaceAll('\r\n', '\n');
function contains(parent, child) {
  const path = relative(parent, child);
  return path === '' || (!isAbsolute(path) && path !== '..' && !path.startsWith(`..${sep}`));
}
function git(...args) {
  return execFileSync('git', ['-C', root, ...args], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] });
}
function commit(value) {
  if (!/^[0-9a-f]{40}$/i.test(value ?? '')) throw new Error('更新前・更新後には40桁のコミットSHAを指定してください。');
  const resolved = git('rev-parse', '--verify', `${value}^{commit}`).trim();
  if (resolved !== value.toLowerCase()) throw new Error('コミット自体のSHAを指定してください。');
  return resolved;
}

try {
  if (process.argv.length !== 5) throw new Error('使い方: node scripts/update-common.mjs <採用先> <更新前のコミットSHA> <更新後のコミットSHA>');
  const destination = realpathSync(resolve(process.argv[2]));
  if (!lstatSync(destination).isDirectory() || contains(realpathSync(root), destination)) {
    throw new Error('採用先は配布元リポジトリの外にある既存ディレクトリを指定してください。');
  }
  const before = commit(process.argv[3]);
  const after = commit(process.argv[4]);
  const changes = files.map(file => {
    const path = resolve(destination, file);
    for (let parent = dirname(path); parent !== destination; parent = dirname(parent)) {
      const stat = lstatSync(parent, { throwIfNoEntry: false });
      if (stat && (!stat.isDirectory() || stat.isSymbolicLink() || !contains(destination, realpathSync(parent)))) {
        throw new Error(`通常のプロジェクト内ディレクトリが必要です: ${file}`);
      }
    }
    const stat = lstatSync(path, { throwIfNoEntry: false });
    const existed = git('ls-tree', '--name-only', before, '--', `template/${file}`).trim() !== '';
    if (existed) {
      if (!stat?.isFile() || stat.nlink !== 1 || !contains(destination, realpathSync(path))) {
        throw new Error(`通常のプロジェクト内ファイルが必要です: ${file}`);
      }
      const original = normalize(git('show', `${before}:template/${file}`));
      const current = normalize(readFileSync(path, 'utf8'));
      if (current !== original) throw new Error(`更新前の原本と一致しません。変更内容を確認してください: ${file}`);
    } else if (stat) {
      throw new Error(`新しい配布資材の配置先が既に存在します: ${file}`);
    }
    return { path, text: normalize(git('show', `${after}:template/${file}`)) };
  });
  // All source and destination files are checked before the first write.
  for (const change of changes) {
    mkdirSync(dirname(change.path), { recursive: true });
    writeFileSync(change.path, change.text, 'utf8');
  }
  console.log(`共通資材${files.length}件を ${after} に差し替えました。`);
  console.log('採用先で python scripts/doc_check.py . を実行し、必要な書式移行を完了してから docs/document-policy.md の採用元固定コミットを更新してください。');
} catch (error) {
  console.error(error.stderr?.toString().trim() || error.message);
  process.exitCode = 1;
}
