# React / ASP.NET Core アーキテクチャ

[全体構成](architecture.md)の技術固有の判断を記録します。ユースケースの振る舞いは各 UC、実現パターンは `design/UCP-n.md`、テーブルと値の制約は[データ設計](design/data.md)を正本とします。

本書の実装パスは `reference/` を起点とします。製品の現行設計は生成先ルートの `docs/` で管理します。

## 1. 実行単位と責務

運用時と Visual Studio の F5 では `backend/App.csproj` が React をビルド・配置し、UI と API を単一の .NET プロセスから同じ origin で配信します。コンソール開発では `mise run dev` が Vite と .NET を別々に起動し、Vite が API と SSE をプロキシします。どちらも実 SQLite を使用します。`frontend/Frontend.esproj` は Visual Studio のソリューション表示用で、UI ビルドは所有しません。

フロントエンドの `usecases/` は対話と下書き、`features/` は API 呼び出しとイベント購読を担当します。バックエンドは画面単位ではなく API エンドポイント単位で `backend/Features/Notes/` にファイルを分けます。画面やユースケースと API は 1:1 に対応させません。

## 2. API 単位の実装

各 API ファイルにはルート登録の `Map` と、責務を明示する内部 `PresentationLayer`、`ApplicationLayer` を置きます。DB を使う API には、1関数が1つの SQL を実行する static `PersistenceLayer` も置きます。`ApplicationLayer` は共通の `IApplicationLayer<TRequest, TResult>` を実装し、トランザクションと業務判断を担当します。`PresentationLayer` は公開応答への変換を担当します。DB を使わない `PreviewNotes` に永続化レイヤーは置きません。

テストで差し替える関数は、それを呼ぶレイヤーの `Func` または `Action` メンバーに既定実装を保持します。`Database` は起動時に生成して必要な `ApplicationLayer` に渡し、static `PersistenceLayer` には状態を持たせません。API 専用の入出力型は担当ファイルに置き、プレビューと一括登録が共有する `BulkInput` だけを独立させます。一括登録はプレビューのアプリケーション処理を再利用し、確認時と確定時に同じ内容を検証します。

API 間で共有する DataAnnotations 属性は `backend/Presentation/Http/Validation/` に1クラス1ファイルで置きます。現在の対象は `NoteTitleAttribute`、`RuneMaxLengthAttribute`、`UuidAttribute` です。認証、永続化、通知などの共通処理は、それぞれ `Infrastructure/Authentication/`、`Infrastructure/Persistence/`、`Infrastructure/Notifications/` に置きます。

## 3. HTTP 契約と状態

C# の API 入出力型を契約の正本とし、OpenAPI から `contracts/api.gen.ts` を生成します。React は `contracts/notes.ts` の別名を通じて生成型を使います。保存と削除の入力は DataAnnotations で自動検証し、フィールド別のエラーを標準の Validation Problem Details で返します。保存入力の項目間条件は `IValidatableObject` で定義し、タイトルのトリムは検証後の保存処理で行います。

| HTTP | パス | このアプリでの役割 |
| --- | --- | --- |
| GET | `/api/notes`、`/api/notes/{id}` | 利用者のメモの一覧・単件取得 |
| POST | `/api/notes/save`、`/api/notes/remove` | 版を検査した保存・削除 |
| POST | `/api/notes/preview`、`/api/notes/import` | 一括入力の確認・確定 |
| GET | `/api/session`、`/events/notes` | セッション取得・変更通知 |
| POST | `/api/demo/sign-in`、`/api/demo/sign-out` | ローカル参照用の利用者切替 |

属性検証が必要な保存・削除 POST は、入力型を明示した `MapPost` ハンドラーで登録します。このテンプレートでは汎用 `MapAuthenticatedPost<TRequest>` 経由の削除入力に属性検証が適用されず、不正な UUID が 404 になったためです。認証結果は文字列 ID ではなく `Principal` としてアプリケーションレイヤーへ渡します。

公開エラーは HTTP ステータスと標準の Problem Details で表し、独自の FaultCode を応答に含めません。保存・削除の対象なしと版競合はアプリケーションの結果型で表し、プレゼンテーションで 404・409 に変換します。確定後にだけ `ChangeNotifications` が利用者のタブへ変更を通知し、購読側の失敗で確定済みの操作を失敗扱いにはしません。下書きは React が保持し、再取得や保存失敗で上書きしません。

<a id="persistence"></a>
## 4. 永続化

`Database` が DB パスと接続の生成を管理し、`ApplicationLayer` が `BeginTransactionAsync` で `ITransaction` を取得して確定します。開始モードは既定の `IMMEDIATE` を必要に応じて指定変更できます。SQL は API ファイル内の `PersistenceLayer` に置き、Dapper で実行します。

新規メモの ID と更新日時は永続化レイヤーが発行し、日時には SQLite の UTC 時刻を使います。INSERT・UPDATE は `RETURNING` で保存後の `Note` を返し、保存後の読み直しを行いません。書込みは利用者の確認待ちを含まない短いトランザクションで確定します。

<a id="test-boundary"></a>
## 5. 並列 E2E と検証境界

E2E は Vite と .NET を分離する `dev`、UI を .NET に同梱する `hosted` の両方で同じシナリオを実行します。各テストは .NET プロセスと一時 SQLite ファイルを分離し、UI 操作後は別接続から確定済みデータを確認します。HTTP 契約テストは両モードとも .NET に直接接続します。同一 DB の競合は一つのテスト内で複数の対話を動かして確認します。

<a id="deployment"></a>
## 6. 配備・認証

`mise run package` は UI を含む .NET 配布物を作り、DB 領域は配布物から分離します。配布先に Node.js は不要です。`AUTH_MODE=demo` はローカル参照用であり、共有環境では既存の認証プロキシと HTTPS を使い、検証済みの利用者 ID のみをサーバーに渡します。
