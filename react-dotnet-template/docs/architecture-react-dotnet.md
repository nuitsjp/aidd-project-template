# React / ASP.NET Core アーキテクチャ

[architecture.md](architecture.md) の技術固有構造を補足する文書です。ユースケース固有の仕様は各 UC、実現パターンは `design/UCP-n.md`、保存方式とテーブルは [データ設計](design/data.md) に記録し、本書への転記は行いません。

## 1. 構成・責務

React・TypeScript・Vite、TanStack Router/Query、Mantine・CSS Modules を用いる SPA と、HTTP JSON と SSE を提供する ASP.NET Core（.NET 10）で構成します。運用時と Visual Studio の F5 は、`backend/App.csproj` が UI をビルドして静的ファイルを配置し、単一の .NET プロセスから UI と API を同じ origin で配信します。コンソール開発では `mise run dev` が Vite（`127.0.0.1:5173`）と ASP.NET Core（`127.0.0.1:3000`）を起動し、Vite の HMR と `/api`・`/events`・`/health` のプロキシを使用します。開発時も実 DB を使用します。

対話制御はユースケース中心、バックエンドは画面非依存の機能領域中心に分離します。ユースケースと機能は n:n で対応し、Service や画面との 1:1 対応は要求しません。

| 配置 | 責務 |
| --- | --- |
| `frontend/src/app/`, `frontend/src/routes/` | Provider・Router・画面の組み立て |
| `frontend/src/usecases/` | 対話進行、下書き保持、機能の組み合わせ |
| `frontend/src/features/` | HTTP JSON 呼び出し、Query、機能別イベント購読 |
| `frontend/src/shared/` | 機能非依存の共用 UI |
| `contracts/notes.ts` | ブラウザ側の公開入出力型 |
| `frontend/Frontend.esproj` | ソリューション上で frontend を表示するための登録。依存取得と UI ビルドは所有しない |
| `backend/App.csproj` | ASP.NET Core サーバー、UI ビルド、静的 UI 配信の実行単位 |
| `backend/Features/Notes/SaveNote.cs` | メモ保存 API の HTTP 受付、入力検証、業務判断、SQL、トランザクションをまとめたクラス |
| `backend/Features/Notes/NotesService.cs` | 保存 API 以外のメモの業務判断、SQL、トランザクションをまとめた機能サービス |
| `backend/Domain/` | `AppFaultException` を置くドメイン名前空間 |
| `backend/Domain/Notes/` | `Note` と `NoteRules` を置くドメイン名前空間 |
| `backend/Application/Authentication/` | `Principal` を置くアプリケーション名前空間 |
| `backend/Infrastructure/Persistence/` | `AppDatabase` と SQLite マイグレーションを置く永続化名前空間 |
| `backend/Infrastructure/Authentication/` | `IdentityService` を置く認証名前空間 |
| `backend/Infrastructure/Notifications/` | `ChangeNotifications` を置く通知名前空間 |
| `backend/Infrastructure/Configuration/` | `AppConfig` を置く設定名前空間 |
| `backend/Presentation/Http/` | `ApiEndpoints`、`JsonInput`、`JsonRequest`、HTTP エラー型を置く HTTP プレゼンテーション名前空間 |
| `tests/backend/Backend.Tests.csproj` | サーバー境界と実 DB を使う .NET テスト |

呼び出し経路は `usecases → features → HTTP JSON → ASP.NET Core エンドポイント → NotesService または SaveNote → Dapper / Microsoft.Data.Sqlite` とし、フロントエンドは公開契約と型定義のみを参照します。メモ保存 API だけは `SaveNote.cs` に API 単位で分離し、その他のメモ API は `NotesService.cs` にまとめます。API 専用の C# record は担当するファイルに同居させます。`AppFaultException` は `Aidd.ReactDotnet.Domain`、`Note` とタイトル・本文の検証規則 `NoteRules` は `Aidd.ReactDotnet.Domain.Notes`、`Principal` は `Aidd.ReactDotnet.Application.Authentication`、`AppDatabase` とマイグレーションは `Aidd.ReactDotnet.Infrastructure.Persistence`、`IdentityService` は `Aidd.ReactDotnet.Infrastructure.Authentication`、`ChangeNotifications` は `Aidd.ReactDotnet.Infrastructure.Notifications`、`AppConfig` は `Aidd.ReactDotnet.Infrastructure.Configuration`、`JsonRequest`、HTTP エラー型、`ApiEndpoints`、`JsonInput` は `Aidd.ReactDotnet.Presentation.Http` 名前空間に置きます。汎用 Repository、多層クラス、独自の DI コンテナは設けません。`NotesService` と `SaveNote` の純粋な `internal static` メソッドは直接呼び出し、DB などの I/O 境界だけをインスタンスごとの `internal` な `Func` で差し替えます。既定の I/O 実装は同じファイルの `internal static` 実装とし、static の共有可変状態は持ちません。

## 2. HTTP 契約と状態

画面とサーバーは `contracts/notes.ts` に定義した JSON 形状を境界とし、サーバー側の C# DTO と境界テストで対応を確認します。公開エラーは次の形で返します。

```json
{"appError":{"code":"...","message":"...","fieldErrors":{"title":"..."}}}
```

`fieldErrors` は必要な場合だけ含めます。

| メソッド | パス | 役割 |
| --- | --- | --- |
| GET | `/api/notes` | 現在の利用者のメモ一覧を取得 |
| GET | `/api/notes/{id}` | 現在の利用者のメモを 1 件取得 |
| POST | `/api/notes/save` | メモを新規作成または版を検査して更新 |
| POST | `/api/notes/remove` | 現在の利用者のメモを削除 |
| POST | `/api/notes/preview` | 一括登録内容を検証してプレビュー |
| POST | `/api/notes/import` | 確認済み入力を再検証し一括登録 |
| GET | `/api/session` | 現在のセッションと認証モードを取得 |
| POST | `/api/demo/sign-in` | `demo` モードで利用者を選択 |
| POST | `/api/demo/sign-out` | `demo` モードのセッションを終了 |
| GET | `/events/notes` | 確定後のメモ変更を SSE で通知 |

業務状態はサーバー、キャッシュは Query、遷移状態は Router、下書きは React が所有します。複数段階の対話では共通親が下書きを保持し、保存失敗や再取得で下書きを上書きしません。

成功応答と変更通知は DB 確定後に返します。保存後の再取得や SSE の失敗で、確定済み保存を失敗扱いにはしません。所有者条件と版検査はサーバー側の機能サービスで行います。

<a id="persistence"></a>
## 3. 永続化と起動単位

ASP.NET Core の起動インスタンスが設定、認証、通知、機能サービスを所有します。`DB_PATH` は起動時に注入し、static の共有可変状態で切り替えません。アプリケーションはマイグレーション成功後に listen を開始し、終了時は SSE 購読を閉じて処理を drain し、DB 資源を解放します。F5 と hosted 起動では `App.csproj` のビルド処理が React の成果物を .NET の静的ファイル領域へ配置します。`Frontend.esproj` はソリューション表示用で、UI ビルドの責務を持ちません。

SQL の実行と結果のマッピングには Dapper を使い、SQL は各 API の実装ファイルに置きます。`BEGIN IMMEDIATE`・`COMMIT`・`ROLLBACK` は共通の `AppDatabase.BeginImmediate`・`Commit`・`Rollback` に集約します。DB 操作ごとに `Microsoft.Data.Sqlite` の接続を開き、WAL、外部キー制約、有限の busy timeout を設定します。書込みは外部 I/O や利用者の確認待ちを含まない短いトランザクションで確定します。SQL 移行は `backend/Infrastructure/Persistence/Migrations` に置きます。保存時のテーブルと値の制約は [データ設計](design/data.md) に従います。

`.NET 10 SDK 10.0.401`、Node.js `24.21.0`、Python `3.13.15` は生成先の `mise.toml` の `[tools]` で固定します。Node.js は Vite の開発サーバー、UI のビルド、Vitest、Playwright、独立した DB 確認に使用します。配布物の起動に Node.js は必要ありません。

<a id="test-boundary"></a>
## 4. 並列 E2E

E2E は同じシナリオを `dev` と `hosted` の2つの起動形式で実行します。`mise run test:e2e:dev` はバックエンドだけをビルドし、Vite の開発サーバー、HMR、API プロキシを経由して実 DB を操作します。`mise run test:e2e:hosted` は UI と API を一体ビルドし、ビルド済み UI を配信する単一 .NET プロセスで同じシナリオを実行します。

どちらの形式でも、1 テストごとにブラウザ Context、.NET プロセス、OS 自動割当ポート、一時 SQLite ファイル、Cookie、セッション、SSE 購読を分離します。dev 形式のブラウザ試験だけが、追加で個別の Vite サーバーとキャッシュを使用します。HTTP 契約の試験は両形式とも .NET に直接接続します。サイズ上限超過などで上流が接続を閉じた場合、Vite の開発プロキシはAPIの応答をそのまま中継できないためです。マイグレーションと業務処理は本番と同じ .NET コードを実行し、UI 操作後は独立した Node.js `node:sqlite` の読取専用接続でコミット済みデータを確認します。fixture は成否を問わずサーバー停止、接続解放、一時領域削除を行います。共有 DB の全削除、テスト用リセット API、外側トランザクションは使用しません。

テスト間分離と同一 DB 競合の検証は区別します。同一 DB の競合は 1 テスト内で複数 Page または Context を作成して検証します。

<a id="deployment"></a>
## 5. 配備・認証

本番環境は同一版の UI と ASP.NET Core サーバーを一体で配備し、DB 領域を配布物から分離します。`mise run package` は `dist/server` の内容と `LICENSE` を `release/app` 直下へ配置し、配布先では .NET 10 ASP.NET Core runtime の `dotnet App.dll` で起動します。配布先に Node.js は要求しません。

`AUTH_MODE=demo` の利用者選択はローカル参照用であり、認証ではありません。共有環境では認証プロキシと HTTPS を経由させ、クライアント由来の認証ヘッダーを除去してから検証済みの利用者 ID だけを API へ渡します。ID 基盤はアプリ内へ再実装しません。プロキシ側で TLS 終端、SSE バッファリング無効化、適切なタイムアウトを設定します。
