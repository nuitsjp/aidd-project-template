# React Templateのプロジェクト定義

## 1. 目的と範囲

React の対話制御から実 Node.js・SQLite への更新までを通す参照実装です。メモの編集と一括登録の2つのパターン、および個別 DB による並列 E2E を提供します。

<a id="constraints"></a>
## 2. 制約・受け入れ条件

SPA、単一 Node.js、同一 origin、SQLite を既定とします。各利用者のメモは所有者 ID で分離します。4 worker の E2E において、同一ユーザー・同一タイトルを用いてもテスト間で干渉しないこと、UI から実 API を経由して実ファイル DB を別接続で検証することを条件とします。

一括登録の上限はサンプルとして100件です。SQLite の配置とトランザクション境界は [React / Node.jsアーキテクチャの永続化と起動単位](architecture-react.md#persistence) に従います。

<a id="usecases"></a>
## 3. ユースケース一覧

| UC ID | 主アクター | 目的 | 実装順序 | 実現パターン | モック適用 |
| --- | --- | --- | --- | --- | --- |
| [UC-1](usecases/UC-1.md) | 利用者 | メモを作成・編集して保存する | 1 | [UCP-1](architecture.md#patterns) | 対象 |
| [UC-2](usecases/UC-2.md) | 利用者 | 複数のメモを確認して一括登録する | 2 | [UCP-2](architecture.md#patterns) | 対象 |

<a id="design"></a>
## 4. 確認した事実

共通資材の配布元と適用版は [文書方針](document-policy.md#adoption) に従います。直接依存は `package.json`、Node 推奨版は `.nvmrc` に記載しています。

- **Node 24 node:sqlite**: Release Candidate 版。実 SQLite を用いる処理・マイグレーション・バックアップを同一ドライバーで検証（[Node API](https://nodejs.org/docs/latest-v24.x/api/sqlite.html)）。並列 E2E の分離は DB ファイルの分離により実現（[SQLite WAL](https://sqlite.org/wal.html)）。
- **Playwright fixtures**: 環境生成と破棄を一体化し、fullyParallel と複数 worker を利用（[fixtures](https://playwright.dev/docs/test-fixtures)）。
- **tRPC / Fastify**: Fastify アダプターと型付きクライアントを使用（[Fastify adapter](https://trpc.io/docs/server/adapters/fastify)）。

<a id="commands"></a>
## 5. 実行・切り替え・検証手順

生成したプロジェクトのルートを作業ディレクトリとします。Node.js 24.21.0 を推奨し、最低22.16、文書検査に Python 3 を使用します。Docker や外部 DB は不要です。

同じルートで依存導入、`.env` 作成、ルート生成を行います。初回生成された `package-lock.json` を保存し、以後は `npm ci` を使用します。

```sh
npm run setup
npm run dev
```

ブラウザで `http://127.0.0.1:5173/notes` を開きます。ユーザー選択（Alice/Bob）でメモの作成・編集・一括登録を試せます。既定のユーザー選択はローカル参照用であり認証ではありません。共有環境では [認証・配備](#deployment) を設定します。

本番形式では次を実行し、`http://127.0.0.1:3000/notes` を開きます。ビルド済み UI・tRPC・SSE を同一 origin で利用し、停止は Ctrl+C とします。DB は既定で `data/app.sqlite` に保存され、停止・再起動後もデータを維持します。

```sh
npm run build
npm start
```

ユーザー操作から DB までの E2E は、初回のみ Chromium を導入してから実行します。

```sh
npx playwright install chromium
npm run test:e2e
```

4 worker で実行します。テストごとのプロセス・DB 分離、UI 操作後の DB 確認、同一 DB の競合境界は [アーキテクチャのテスト分離](architecture.md#test-boundary) に従います。4並列の反復、全体検証、実 SQLite の機能・プロセス分離は次で実行します。

```sh
npm run test:e2e:repeat
npm run verify
npm run test:core
```

worker 数を変更する場合は `npx playwright test --workers=8` を使用します（事前ビルドが必要）。共有 DB を使わない実行条件と同一 DB の競合境界は [アーキテクチャのテスト分離](architecture.md#test-boundary) を参照します。

テスト失敗時のアーティファクトは `.e2e-results/` 等に配置され、試験 DB は fixture が自動削除します。リポジトリに実データや診断ログを含めません。

仕様確認用モックが必要な期間のみ `frontend/src/mocks/notes.ts` を作成し、`npm exec -- vite --config frontend/vite.config.ts --mode mock` で起動します（本番ビルドでのモック使用は禁止）。実処理へ切り替えた後は固定データを削除し、E2E は実 API で検証します。

変更後は `npm run verify` を実行し、型検査・Lint・文書・機能・UI・ビルド・E2E がすべて合格した状態を維持します。

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

本番成果物は `frontend/dist` と `dist/backend` です。配備先では同一 Node 版で `npm ci --omit=dev` を実行し、DB 領域は配備ディレクトリの外側に配置します。

### DB運用

マイグレーションは `backend/db/migrations` に配置し、適用済み SQL は変更しません（現行 `user_version=1`）。

```sh
npm run db:backup -- ./backups/manual.sqlite
npm run db:check -- ./backups/manual.sqlite
```

バックアップは `VACUUM INTO` で整合性のあるスナップショットを作成します。復元時はサーバー停止後、既存 DB と WAL/SHM を退避し、チェック済みバックアップを配置して起動します。
