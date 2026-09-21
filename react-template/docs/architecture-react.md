# React / Node.jsアーキテクチャ

[architecture.md](architecture.md) の共通構造を補足する文書です。ユースケース固有の仕様、テーブル設計、実現パターンは同書および各 UC に集約し、本書への転記は行いません。

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

成功応答と変更通知は DB 確定後に返します。要求と結果は tRPC、変更通知は必要な機能のみ SSE で扱います（保存後の再取得失敗で確定済みの保存を失敗扱いにはしません）。

公開エラーはコード、安全なメッセージ、必要な入力項目情報で返します。内部要因は API 境界で記録し、利用者への表示や再試行対話はフロントエンド側で制御します。

## 3. 永続化と起動単位

永続化には SQLite を使用します。ファイルはサーバーの永続領域に配置し、WAL、外部キー制約、有限の busy timeout を設定して短いトランザクションで確定します（同期 DB 操作中に外部 I/O や確認待ちは含めません）。

`createApp(config)` ごとに接続・Service・通知・セッションを生成します。DB パスは設定として注入し、グローバル変数での切り替えは行いません。初期化・マイグレーション成功後に listen を開始し、終了時は SSE を閉じ、処理を drain して DB を解放します。

ドライバーには Node 標準の `node:sqlite` を使用します。Node 24 系列での API 安定度（Release Candidate）を考慮し、配備先の Node 版を固定します。汎用 DB 抽象化層の先行実装は行いません。
