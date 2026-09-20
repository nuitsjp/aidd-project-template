# AIDD React Template

Reactの対話制御からNode.jsの機能、SQLiteへの確定までを通す参照実装です。

**検証状態:** 実SQLiteによる機能・並列分離テスト21件は合格。作成環境の外部接続制約により、依存解決・全体ビルド・Playwright E2Eは未検証です（詳細は [検証結果](docs/project.md#verification) 参照）。

## 配置

`react-template/` は、同一チェックアウトの `template/` に重ねるReact固有の差分です。単独実行せず、リポジトリのルートから以下を実行して新規プロジェクトを生成します（`template/` を先にコピーし、`react-template/` の内容で上書きしてルートの `LICENSE` を配置します）。

```text
aidd-project-template/
├── template/
└── react-template/       ← 生成時に重ねる差分（単独実行しない）
```

```powershell
mise run init:react ../my-react-app
```

## 開始

生成されたプロジェクトのルートでコマンドを実行します。Node.js 24.21.0推奨（最低22.16）、文書検査にPython 3を使用します（Dockerや外部DBは不要）。

```sh
npm run setup
npm run dev
```

ブラウザで `http://127.0.0.1:5173/notes` を開きます。ユーザー選択（Alice/Bob）でメモの作成・編集・一括登録を試せます。既定のユーザー選択は**ローカル参照実装用であり認証ではありません**。共有環境への配備時は [認証・配備](docs/project.md#deployment) を設定してください。

セットアップは依存導入、`.env`作成、ルート生成を行います。初回生成された `package-lock.json` を保存し、以後は `npm ci` を使用します。

## ビルドと本番形式でのローカル起動

```sh
npm run build
npm start
```

`http://127.0.0.1:3000/notes` から、ビルド済みUI・tRPC・SSEを同一originで利用します（停止は Ctrl+C）。DBは既定で `data/app.sqlite` に保存され、停止・再起動後もデータは維持されます。

## ユーザー操作からDBまでの並列E2E

```sh
npx playwright install chromium
npm run test:e2e
```

4 workerで実行します。**1テストごとに、ブラウザContext・本番Nodeプロセス・自動割当ポート・一時SQLiteファイルを新規作成**します。本番マイグレーションから初期化し、UIからの保存後、独立した読取専用接続で更新結果を検証します。

```sh
# 4並列で同じ試験を3回繰り返す（再試行による失敗隠蔽を防止）
npm run test:e2e:repeat
# 型・Lint・文書・機能・UI・ビルド・E2Eを一括実行
npm run verify
# npm依存なしで実SQLite機能・4プロセス分離を検証
npm run test:core
```

worker数変更時は `npx playwright test --workers=8`（事前ビルド要）。テスト間は完全に分離され、共有DBの全削除やテスト用リセットAPI、外側ロールバックは使用しません。同一DBの競合試験は、**1テスト内で複数Context/Pageを作成**して検証します。

## 参照先

| 文書 | 内容 |
| --- | --- |
| [architecture.md](docs/architecture.md) | 実装パターン、保存の確定点、テーブル、テスト分離 |
| [architecture-react.md](docs/architecture-react.md) | 共通の責務と依存方向 |
| [project.md](docs/project.md) | サンプルの範囲、認証・配備・DB運用、検証結果 |
| [document-policy.md](docs/document-policy.md) | 輸入標準と今回の提供範囲・合意状態 |

サンプルのUIやタイトル一意制約、文字数制限は参照用仕様です。製品開発時は `backend/features/notes/`・`frontend/src/usecases/` とE2Eを製品のユースケースに置き換えます。

サンプルの仕様・合意は採用先へ引き継がず、採用時は [導入開始手順](https://github.com/nuitsjp/aidd-project-template#3-初期セットアップと最初のユースケース) を確認して製品固有の仕様と合意を定義します。

`.github/workflows/react-template.yml` は生成プロジェクト用のCI例です（生成後のルートで実行することを前提としています）。
