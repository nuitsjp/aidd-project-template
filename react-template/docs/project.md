# React Templateのプロジェクト定義

## 1. 目的と範囲

React の対話制御から実 Node.js・SQLite への更新までを通す参照実装です。メモの編集と一括登録の2つのパターン、および個別 DB による並列 E2E を提供します。

<a id="constraints"></a>
## 2. 制約・受け入れ条件

SPA、単一 Node.js、同一 origin、SQLite を既定とします。各利用者のメモは所有者 ID で分離します。4 worker の E2E において、同一ユーザー・同一タイトルを用いてもテスト間で干渉しないこと、UI から実 API を経由して実ファイル DB を別接続で検証することを条件とします。

一括登録の上限はサンプルとして100件です。SQLite の配置、保存方式、トランザクション境界は [データ設計](design/data.md) に従います。

<a id="usecases"></a>
## 3. ユースケース一覧

| ユースケース | 主アクター | 目的 | 実装順序 | 実現パターン | モック適用 |
| --- | --- | --- | --- | --- | --- |
| [メモを作成・編集して保存する](usecases/メモを作成・編集して保存する/README.md) | 利用者 | メモを作成・編集して保存する | 1 | [UCP-1](design/UCP-1.md) | 対象 |
| [複数のメモを確認して一括登録する](usecases/複数のメモを確認して一括登録する/README.md) | 利用者 | 複数のメモを確認して一括登録する | 2 | [UCP-2](design/UCP-2.md) | 対象 |

<a id="design"></a>
## 4. 確認した事実

共通資材の配布元と採用元固定コミットは [文書方針](document-policy.md#adoption) に従います。直接依存は `package.json`、Node.js の指定版は `.nvmrc` に記載しています。

- **Node.js / SQLite**: Node.js 24.21.0 と同版の標準 `node:sqlite` を使用します。実 SQLite を用いる処理、マイグレーション、バックアップを同一ドライバーで実行します（[Node API](https://nodejs.org/docs/latest-v24.x/api/sqlite.html)）。並列 E2E は DB ファイルを分離します（[SQLite WAL](https://sqlite.org/wal.html)）。
- **Playwright fixtures**: 環境生成と破棄を一体化し、fullyParallel と複数 worker を利用します（[fixtures](https://playwright.dev/docs/test-fixtures)）。
- **tRPC / Fastify**: Fastify アダプターと型付きクライアントを使用します（[Fastify adapter](https://trpc.io/docs/server/adapters/fastify)）。

<a id="commands"></a>
## 5. 実行・切り替え・検証手順

生成したプロジェクトのルートを作業ディレクトリとします。Node.js は `.nvmrc` と `package.json` に固定した24.21.0を使用し、文書検査に Python 3 を使用します。Docker や外部 DB は不要です。nvm を使う場合は指定版を導入して選択します。nvm-windows では版番号を明示してください。

```powershell
$nodeVersion = (Get-Content .nvmrc -Raw).Trim()
nvm install $nodeVersion
nvm use $nodeVersion
```

セットアップは同梱の `package-lock.json` を使って `npm ci` を実行し、`.env` とルートツリーを生成します。ロックがなければ停止します。依存を更新する場合だけ `package.json` と `package-lock.json` を併せて更新し、`npm run setup` と `npm run verify` で確認します。採用後のロックは採用先で管理し、新しい雛形のロックで上書きしません。

依存取得先へ組織のプロキシ経由で接続する場合は、`npm run setup` の前に端末の `HTTPS_PROXY` を設定します。値をリポジトリへ保存せず、TLS 検証を無効化しません。

```sh
npm run setup
npm run dev
```

ブラウザで `http://127.0.0.1:5173/notes` を開きます。ユーザー選択（Alice/Bob）でメモの作成・編集・一括登録を試せます。既定のユーザー選択はローカル参照用であり認証ではありません。共有環境では [認証・配備](#deployment) を設定します。

本番形式では次を実行し、`http://127.0.0.1:3000/notes` を開きます。ビルド済み UI、tRPC、SSE を同一 origin で利用し、停止は Ctrl+C とします。DB は既定で `data/app.sqlite` に保存され、停止・再起動後もデータを維持します。

```sh
npm run build
npm start
```

ユーザー操作から DB までの E2E は、初回のみ導入済み Playwright に対応する Headless Shell を取得してから実行します。

```sh
npx playwright install --only-shell chromium
npm run test:e2e
```

ブラウザ実体を明示する場合は `PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` を指定します。その環境で `--disable-extensions` による起動失敗を確認した場合だけ、`PLAYWRIGHT_IGNORE_DISABLE_EXTENSIONS=1` を加えて同引数を除外します。ブラウザの自動切り替えやフォールバックは行いません。

```powershell
$env:PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH = Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'
$env:PLAYWRIGHT_IGNORE_DISABLE_EXTENSIONS = '1'
npm run test:e2e
```

既定は4 workerです。テストごとの分離、UI 操作後の DB 確認、同一 DB の競合境界は [並列E2E](architecture-react.md#test-boundary) に従います。反復、全体検証、実 SQLite の機能・プロセス分離は次で実行します。

```sh
npm run test:e2e:repeat
npm run verify
npm run test:core
```

worker 数を変更する場合は `npx playwright test --workers=8` を使用します（事前ビルドが必要）。テスト失敗時のアーティファクトは `.e2e-results/` 等に配置され、試験 DB は fixture が自動削除します。実データや診断ログはリポジトリへ含めません。

仕様確認用モックが必要な期間のみ `frontend/src/mocks/notes.ts` を作成し、`npm exec -- vite --config frontend/vite.config.ts --mode mock` で起動します。本番ビルドでのモック使用は禁止します。実処理へ切り替えた後は固定データを削除し、E2E は実 API で検証します。

変更後は `npm run verify` を実行し、型検査、Lint、文書、機能、UI、ビルド、E2E がすべて合格した状態を維持します。

<a id="deployment"></a>
### 認証・配備

`AUTH_MODE=demo` はローカル参照用の簡易ユーザー選択であり、認証ではありません。

共有サーバー配備時は以下を設定して起動します。

```dotenv
HOST=127.0.0.1
PORT=3000
AUTH_MODE=proxy
PUBLIC_ORIGIN=https://app.example.com
DB_PATH=./persistent/app.sqlite
LOG_LEVEL=info
```

Node.js は loopback 限定とします。認証リバースプロキシが全エンドポイントを保護し、クライアントからの `x-authenticated-user` ヘッダーを除去したうえで、検証済みの利用者 ID を付与します。プロキシ側で TLS 終端、SSE バッファリング無効化、適切なタイムアウトを設定します。

`npm run package` は検証後に `release/app` を生成し、`package.json`、`package-lock.json`、`.nvmrc` とビルド成果物を配置します。配備先では `.nvmrc` と同じ Node.js を選択し、`npm ci --omit=dev` を実行します。DB 領域は配備ディレクトリの外側に配置します。

### DB運用

マイグレーションは `backend/db/migrations` に配置し、適用済み SQL は変更しません（現行 `user_version=1`）。

```sh
npm run db:backup -- ./backups/manual.sqlite
npm run db:check -- ./backups/manual.sqlite
```

バックアップは `VACUUM INTO` で整合性のあるスナップショットを作成します。復元時はサーバー停止後、既存 DB と WAL/SHM を退避し、チェック済みバックアップを配置して起動します。
