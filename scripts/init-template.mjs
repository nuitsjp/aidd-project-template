import { copyFileSync, cpSync, mkdirSync, readFileSync, readdirSync, renameSync, writeFileSync } from 'node:fs';
import { basename, dirname, isAbsolute, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const dotnetGeneratedNames = new Set([
  '.vs', 'bin', 'obj', 'TestResults', 'node_modules', 'dist', 'data', 'release',
  'coverage', '.e2e-results', 'playwright-report', '.env', 'mise.local.props',
]);
const productFiles = [
  '.env.example', '.nvmrc', '.prettierignore', '.prettierrc.json', 'App.slnx', 'global.json', 'package.json',
  'package-lock.json', 'eslint.config.mjs', 'playwright.config.ts',
  'vitest.config.ts', 'tsconfig.json', 'tsr.config.json', 'mise.toml',
  'backend', 'frontend', 'contracts', 'tests', 'scripts',
];
const csharpKeywords = new Set([
  'abstract', 'as', 'base', 'bool', 'break', 'byte', 'case', 'catch', 'char', 'checked',
  'class', 'const', 'continue', 'decimal', 'default', 'delegate', 'do', 'double',
  'else', 'enum', 'event', 'explicit', 'extern', 'false', 'finally', 'fixed',
  'float', 'for', 'foreach', 'goto', 'if', 'implicit', 'in', 'int', 'interface',
  'internal', 'is', 'lock', 'long', 'namespace', 'new', 'null', 'object', 'operator',
  'out', 'override', 'params', 'private', 'protected', 'public', 'readonly', 'ref',
  'return', 'sbyte', 'sealed', 'short', 'sizeof', 'stackalloc', 'static', 'string',
  'struct', 'switch', 'this', 'throw', 'true', 'try', 'typeof', 'uint', 'ulong',
  'unchecked', 'unsafe', 'ushort', 'using', 'virtual', 'void', 'volatile', 'while',
]);

function copyProduct(reference, destination, namespace) {
  for (const name of productFiles) {
    cpSync(resolve(reference, name), resolve(destination, name), {
      recursive: true,
      filter: path => !dotnetGeneratedNames.has(basename(path))
        && relative(reference, path).split(sep).join('/') !== 'frontend/src/routeTree.gen.ts'
        && relative(reference, path).split(sep).join('/') !== 'scripts/check-docs.mjs',
    });
  }
  let replacements = 0;
  const projectNames = [
    ['Backend.IntegrationTests', `${namespace}.IntegrationTests`],
    ['Backend.UnitTests', `${namespace}.UnitTests`],
    ['Frontend.esproj', `${namespace}.Frontend.esproj`],
    ['App.slnx', `${namespace}.slnx`],
    ['App.csproj', `${namespace}.csproj`],
    ['App.dll', `${namespace}.dll`],
    ['App.Migrations.', `${namespace}.Migrations.`],
    ['<AssemblyName>App</AssemblyName>', `<AssemblyName>${namespace}</AssemblyName>`],
    ['使用方法: App db:', `使用方法: ${namespace} db:`],
  ];
  const textExtensions = new Set(['.cs', '.csproj', '.esproj', '.slnx', '.mjs', '.ts', '.tsx', '.json', '.toml']);
  function replaceProductNames(directory) {
    for (const entry of readdirSync(directory, { withFileTypes: true })) {
      const path = resolve(directory, entry.name);
      if (entry.isDirectory()) replaceProductNames(path);
      else if (textExtensions.has(entry.name.slice(entry.name.lastIndexOf('.')))) {
        const original = readFileSync(path, 'utf8');
        let updated = original;
        if (entry.name.endsWith('.cs') || entry.name.endsWith('.csproj'))
          updated = updated.replaceAll('NotesSample', namespace);
        for (const [before, after] of projectNames) updated = updated.replaceAll(before, after);
        if (updated !== original) {
          writeFileSync(path, updated);
          replacements++;
        }
      }
    }
  }
  for (const name of productFiles) {
    const path = resolve(destination, name);
    if (['backend', 'frontend', 'contracts', 'tests', 'scripts'].includes(name)) replaceProductNames(path);
    else if (textExtensions.has(name.slice(name.lastIndexOf('.')))) {
      const original = readFileSync(path, 'utf8');
      let updated = original;
      for (const [before, after] of projectNames) updated = updated.replaceAll(before, after);
      if (updated !== original) writeFileSync(path, updated);
    }
  }
  if (replacements === 0) throw new Error('製品用名前空間の置換対象が見つかりません。');
  const launchSettings = resolve(destination, 'backend/Properties/launchSettings.json');
  writeFileSync(launchSettings, readFileSync(launchSettings, 'utf8').replace('"App": {', `"${namespace}": {`));
  for (const testDirectory of ['dotnet-unit', 'dotnet-integration']) {
    const testLock = resolve(destination, `tests/${testDirectory}/packages.lock.json`);
    writeFileSync(testLock, readFileSync(testLock, 'utf8').replace('"app": {', `"${namespace.toLowerCase()}": {`));
  }
  const openApi = resolve(destination, 'contracts/openapi.json');
  writeFileSync(openApi, readFileSync(openApi, 'utf8')
    .replace('"App | v1"', `"${namespace} | v1"`)
    .replaceAll('"App"', `"${namespace}"`));
  for (const [before, after] of [
    ['App.slnx', `${namespace}.slnx`],
    ['backend/App.csproj', `backend/${namespace}.csproj`],
    ['tests/dotnet-unit/Backend.UnitTests.csproj', `tests/dotnet-unit/${namespace}.UnitTests.csproj`],
    ['tests/dotnet-integration/Backend.IntegrationTests.csproj', `tests/dotnet-integration/${namespace}.IntegrationTests.csproj`],
    ['frontend/Frontend.esproj', `frontend/${namespace}.Frontend.esproj`],
  ]) renameSync(resolve(destination, before), resolve(destination, after));
  const packageName = namespace.toLowerCase().replaceAll('.', '-').replaceAll('_', '-');
  const originalPackageName = JSON.parse(readFileSync(resolve(reference, 'package.json'), 'utf8')).name;
  for (const name of ['package.json', 'package-lock.json']) {
    const path = resolve(destination, name);
    writeFileSync(path, readFileSync(path, 'utf8').replaceAll(JSON.stringify(originalPackageName), JSON.stringify(packageName)));
  }
}

try {
  const kind = process.argv[2];
  if (!['wails', 'react', 'react-dotnet'].includes(kind)
    || (kind === 'react-dotnet' ? process.argv.length !== 6 || process.argv[4] !== '--name' : process.argv.length !== 4)) {
    throw new Error('使い方: mise run init:wails|init:react <新しい出力先> / mise run init:react-dotnet <新しい出力先> --name Company.Product');
  }
  const namespace = kind === 'react-dotnet' ? process.argv[5] : undefined;
  if (kind === 'react-dotnet' && !namespace.split('.').every(part => /^[A-Za-z_][A-Za-z0-9_]*$/.test(part) && !csharpKeywords.has(part))) {
    throw new Error('プロジェクト名は、予約語を除くASCII英数字とアンダースコアのドット区切りC#名前空間で指定してください。');
  }
  const destination = resolve(process.argv[3]);
  const sources = ['template', `${kind}-template`].map(name => resolve(root, name));
  for (const source of ['template', 'wails-template', 'react-template', 'react-dotnet-template'].map(name => resolve(root, name))) {
    const path = relative(source, destination);
    if (path === '' || (!isAbsolute(path) && path !== '..' && !path.startsWith(`..${sep}`))) {
      throw new Error('出力先はtemplate/・wails-template/・react-template/・react-dotnet-template/の外に指定してください。');
    }
  }
  // mkdir fails for an existing destination, before any files are copied.
  mkdirSync(destination);
  for (const source of sources) cpSync(source, destination, {
    recursive: true,
    filter: path => basename(path) !== '.vs' && (basename(source) !== 'react-dotnet-template' ||
      (!dotnetGeneratedNames.has(basename(path)) &&
        relative(source, path).split(sep).join('/') !== 'reference/frontend/src/routeTree.gen.ts')),
  });
  copyFileSync(resolve(root, 'LICENSE'), resolve(destination, 'LICENSE'));
  if (kind === 'react-dotnet')
    copyProduct(resolve(root, 'react-dotnet-template/reference'), destination, namespace);
  console.log(`${kind}の初期状態を生成しました: ${destination}`);
} catch (error) {
  console.error(error.code === 'EEXIST' ? '出力先が既に存在します。新しい名前を指定してください。' : error.message);
  process.exitCode = 1;
}
