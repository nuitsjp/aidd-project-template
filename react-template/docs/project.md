# React Templateのプロジェクト定義

## 1. 目的と範囲

React の対話制御から実 Node.js・SQLite への更新までを通す参照実装です。メモの編集と一括登録の2つのパターン、および個別 DB による並列 E2E を提供します。

## 2. 制約・受け入れ条件

SPA、単一 Node.js、同一 origin、SQLite を既定とします。各利用者のメモは所有者 ID で分離します。4 worker の E2E において、同一ユーザー・同一タイトルを用いてもテスト間で干渉しないこと、UI から実 API を経由して実ファイル DB を別接続で検証することを条件とします。

一括登録の上限はサンプルとして100件です。SQLite はサーバーのローカルディスクに配置し、トランザクション中に非同期 I/O を待たない短い同期処理とします。

<a id="usecases"></a>
## 3. ユースケース一覧

| UC ID | 主アクター | 目的 | 実装順序 | 実現パターン | モック適用 |
| --- | --- | --- | --- | --- | --- |
| [UC-1](usecases/UC-1.md) | 利用者 | メモを作成・編集して保存する | 1 | [UCP-1](architecture.md#patterns) | 対象 |
| [UC-2](usecases/UC-2.md) | 利用者 | 複数のメモを確認して一括登録する | 2 | [UCP-2](architecture.md#patterns) | 対象 |

<a id="design"></a>
## 4. 確認した事実と採用差分

共通資材は同一チェックアウトの `template/` から取得します（採用版は [文書方針](document-policy.md#adoption) に記録）。直接依存は `package.json`、Node 指定版は `.nvmrc` に記載しています。

- **Node 24 node:sqlite**: Release Candidate 版。実 SQLite を用いる処理・マイグレーション・バックアップを同一ドライバーで検証（[Node API](https://nodejs.org/docs/latest-v24.x/api/sqlite.html)）。並列 E2E の分離は DB ファイルの分離により実現（[SQLite WAL](https://sqlite.org/wal.html)）。
- **Playwright fixtures**: 環境生成と破棄を一体化し、fullyParallel と複数 worker を利用（[fixtures](https://playwright.dev/docs/test-fixtures)）。
- **tRPC / Fastify**: Fastify アダプターと型付きクライアントを使用（[Fastify adapter](https://trpc.io/docs/server/adapters/fastify)）。

<a id="commands"></a>
## 5. 実行・切り替え・検証手順

起動および検証コマンドは [README](../README.md) を参照してください。本リポジトリのルートで `mise run init:react ../my-react-app` を実行後、生成先を作業ディレクトリとします（`react-template/` 単体を作業ディレクトリとしません）。

テスト失敗時のアーティファクトは `.e2e-results/` 等に配置され、試験 DB は fixture が自動削除します。リポジトリに実データや診断ログを含めません。

仕様合意用モックが必要な期間のみ `frontend/src/mocks/notes.ts` を作成し、`npm exec -- vite --config frontend/vite.config.ts --mode mock` で起動します（本番ビルドでのモック使用は禁止）。段階4で固定データを削除し、E2E は実 API で検証します。

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

<a id="verification"></a>
## 6. 検証結果

実装後は `npm run verify` を実行し、結果を報告します（未実施の項目は「未検証」と明記）。

| UC・系列 ID | 段階 | 構成 | 実行日 | コマンド | 合否 | 対象コミットまたは CI 参照 |
| --- | --- | --- | --- | --- | --- | --- |
| UC-1-M | — | 本番Node + Chromium + SQLite | — | npm run test:e2e | 未検証 | — |
| UC-1-X1 | — | 同上 | — | npm run test:e2e | 未検証 | — |
| UC-1-X2 | — | 同上 | — | npm run test:e2e | 未検証 | — |
| UC-1-X3 | — | 同上 | — | npm run test:e2e | 未検証 | — |
| UC-1-X4 | — | 同上 | — | npm run test:e2e | 未検証 | — |
| UC-2-M | — | 同上 | — | npm run test:e2e | 未検証 | — |
| UC-2-X1 | — | 同上 | — | npm run test:e2e | 未検証 | — |

合否表の記入と完了条件は [検証結果の記録](standards/design-and-documentation.md#verification-records) に従います。

- **保守検証（2026-09-21、React拡張0.2.3、Windows、Node.js 24.21.0 / SQLite 3.53.0 / Playwright 1.63.0）**: 新規生成先で同梱ロックから `npm run setup` の `npm ci` が成功。`npm run verify` の型検査・Lint・機能テスト21件・単体テスト3件・本番ビルド・E2E 14件は、Playwright headless shell 153.0.8010.12を新規導入した既定環境で合格。`npm run test:e2e:repeat` も42件（14件×3回）が合格。`node scripts/package.mjs` が同じロックを配備物へ同梱し、配備先の `npm ci --omit=dev` も成功。Node版固定は `.nvmrc` の24.21.0を正本とし、setupで実行中のNode版を完全一致検査する。
- **ブラウザ導入の検証**: 端末の既設プロキシを `HTTPS_PROXY` で明示し、空のキャッシュへ `npx playwright install --only-shell chromium` を実行して終了コード0を確認しました。E2Eでは実行ファイルの指定と `PLAYWRIGHT_IGNORE_DISABLE_EXTENSIONS` を解除し、対応版Headless Shellを使用しました。以前の取得失敗はIPv6からIPv4への接続切り替え時のタイムアウト判定によるもので、タイムアウト延長では解消しませんでした。
- **Node版の検証**: Node.js 24.16.0では `setup` が依存導入前に版の不一致で停止することを確認しました。24.16.0に起因するZIP展開停止を避けるため、指定版をローカルとCIで一致させます。配備物にも `.nvmrc` を同梱し、`package.json` の指定版との一致を確認しました。
- **未実施**: 同梱サンプルの系列ごとの動作合意と完成系監査。上表の合否は採用先で段階6を経てから記入します。
