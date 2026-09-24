# React / Node.jsアーキテクチャ

[architecture.md](architecture.md) の技術固有構造を補足する文書です。ユースケース固有の仕様は各 UC、実現パターンは `design/UCP-n.md`、保存方式とテーブルは [データ設計](design/data.md) に記録し、本書への転記は行いません。

## 1. 構成・責務

React・TypeScript・Vite、TanStack Router/Query、Mantine・CSS Modules を用いる SPA と、常駐する単一 Node.js（Fastify/tRPC）で構成します。入力検証は Zod で行い、画面と API は同一 origin で提供します。

対話制御はユースケース中心、バックエンドは画面非依存の機能領域中心に分離します。ユースケースと機能は n:n で対応し、Service や画面との 1:1 対応は要求しません。

| 配置 | 責務 |
| --- | --- |
| `frontend/src/app/`, `routes/` | Provider・Router・画面の組み立て |
| `frontend/src/usecases/` | 対話進行、下書き保持、機能の組み合わせ |
| `frontend/src/features/` | API呼び出し、Query、機能別イベント購読 |
| `frontend/src/shared/` | 機能非依存の共用 UI |
| `contracts/` | 公開入出力型とスキーマ |
| `backend/app.ts` | 接続・機能・HTTP ルートの組み立て |
| `backend/features/` | 業務操作、結果確定、保存処理 |
| `backend/db/` | SQLite 接続とマイグレーション |

呼び出し経路は `usecases → features → tRPC → 機能Service → SQLite` とし、フロントエンドは公開契約と型定義のみを参照します。過剰な多層構造や汎用 Repository、DI コンテナは設けません。

## 2. 状態と更新

業務状態は Node.js、キャッシュは Query、遷移状態は Router、下書きは React が所有します。複数段階の対話では共通親が下書きを保持し、保存失敗や再取得で下書きを上書きしません。

成功応答と変更通知は DB 確定後に返します。要求と結果は tRPC、変更通知は必要な機能のみ SSE で扱います。保存後の再取得失敗で確定済みの保存を失敗扱いにはしません。

公開エラーはコード、安全なメッセージ、必要な入力項目情報で返します。内部要因は API 境界で記録し、利用者への表示や再試行対話はフロントエンド側で制御します。

<a id="persistence"></a>
## 3. 永続化と起動単位

`createApp(config)` ごとに接続、Service、通知、セッションを生成します。DB パスは設定として注入し、グローバル変数での切り替えは行いません。初期化・マイグレーション成功後に listen を開始し、終了時は SSE を閉じ、処理を drain して DB を解放します。

ドライバーには Node 標準の `node:sqlite` を使用し、実行する Node.js は `.nvmrc` と `package.json` の同一版に固定します。保存時の設定とトランザクション条件は [データ設計](design/data.md) に従います。汎用 DB 抽象化層は設けません。

<a id="test-boundary"></a>
## 4. 並列E2E

E2E は `tests/e2e/fixtures.ts` を利用し、1テストごとにブラウザ Context、本番 Node.js プロセス、OS 自動割当ポート、一時 SQLite ファイル、Cookie、セッション、SSE 購読を分離します。全 worker は同一ビルド成果物を使用します。

マイグレーションと業務処理は本番と同じコードを実行し、UI 操作後は独立した読取専用 DB 接続でコミット済みデータを確認します。fixture は成否を問わずサーバー停止、接続解放、一時領域削除を行います。共有 DB の全削除、テスト用リセット API、外側トランザクションは使用しません。

テスト間分離と同一 DB 競合の検証は区別します。同一 DB の競合は1テスト内で複数 Page または Context を作成して検証します。

<a id="deployment"></a>
## 5. 配備・認証

本番環境は同一版の UI とサーバーを一体で配備し、DB 領域を配布物から分離します。利用者はリクエストごとに解決し、所有者条件を保存・取得へ適用します。

demo ユーザー選択はローカル参照用です。共有環境では認証プロキシと HTTPS を経由させ、クライアント由来の認証ヘッダーを除去してから検証済みの利用者情報だけを API へ渡します。ID 基盤は本アプリ内へ再実装しません。
