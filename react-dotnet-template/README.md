# AIDD React + .NET Template

React の対話制御から ASP.NET Core の HTTP API、SQLite への確定までを通す参照実装です。単一の .NET サーバーがビルド済み React UI、HTTP API、SSE を同じ origin で配信します。

## 新規プロジェクトの生成

新規プロジェクトは [配布元の導入手順](https://github.com/nuitsjp/aidd-project-template#2-新規プロジェクトへの導入) に従って生成します。`react-dotnet-template/` は同じチェックアウトの `template/` に重ねる React + .NET 固有の差分です。採用プロジェクトは生成先で開発し、テンプレート開発時はこの拡張ディレクトリを source checkout として単独で実行できます。

```powershell
mise run init:react-dotnet ../my-react-dotnet-app
cd ../my-react-dotnet-app
```

生成時は `template/`、`react-dotnet-template/`、ルートの `LICENSE` の順に配置します。同名ファイルは拡張側で全体を上書きし、既存の出力先には生成しません。共通の文書・検査資材を拡張側へ複製せず、採用先では生成先の資材を使います。

## 実行・確認

生成先の [App.slnx](App.slnx) には、frontend（`frontend/Frontend.esproj`）、バックエンド、バックエンドテストの3プロジェクトを含めています。frontend プロジェクトはソリューション上で UI を表示するための登録であり、UI のビルドと静的ファイルの配置は `backend/App.csproj` が所有します。

生成先では `mise.toml` の `[tools]` が Node.js 24.21.0、.NET SDK 10.0.401、Python 3.13.15 を固定します。生成後のプロジェクトのルートで、内容を確認して `mise trust` を実行し、`mise run setup` でツール、npm 依存、NuGet 依存を導入します。Playwright を使う前に `mise run setup:browser` を一度実行します。

`mise run setup` は PATH 上の mise の実体パスを絶対パスで `backend/mise.local.props` に記録します。このローカルファイルは Git 管理と配布物から除外されます。初回セットアップ時と mise を移動した後に `mise run setup` を再実行してください。

```powershell
mise trust
mise run setup
mise run setup:browser
```

source で作業する場合は、`react-dotnet-template/` を作業ディレクトリにして同じコマンドを実行します。`mise run setup` は初回セットアップ時と mise を移動した後に必要です。`mise run check:docs` と `mise run verify` の文書検査は、共通の `template/` とこの拡張を一時生成先へ配置して行い、アプリの build・test は source で行います。source で `mise run package` を実行する場合はリポジトリルートの `LICENSE` を使います。

Visual Studio で source の F5 を使うときは、`react-dotnet-template/App.slnx` を開き、`backend/App.csproj` の App をスタートアッププロジェクトに設定して F5 を押します。`App.csproj` は `backend/mise.local.props` に記録された mise の絶対パスで `mise exec` を実行するため、Visual Studio 起動時の PATH に mise が含まれていなくても F5 を使えます。F5 のたびに `npm ci` を実行する必要はありません。採用先では生成先ルートの `App.slnx` を開き、同じスタートアップ設定を行います。

起動、切り替え、テスト、配備の手順と全タスクの一覧は [プロジェクト定義の実行手順](docs/project.md#commands) に集約しています。コンソールの `mise run dev` は Vite（`127.0.0.1:5173`）と ASP.NET Core（`127.0.0.1:3000`）を実 DB で起動し、Vite の HMR と API プロキシを使います。Visual Studio の F5 と `mise run start` は UI を内包した単一の .NET プロセスを起動します。

## 参照先

| 文書 | 内容 |
| --- | --- |
| [architecture.md](docs/architecture.md) | 全体構成、実現パターンの適用条件、設計上の制約 |
| [UCP-1](docs/design/UCP-1.md)・[UCP-2](docs/design/UCP-2.md) | 実現パターンごとの具体設計と結果確定点 |
| [data.md](docs/design/data.md) | 保存方式、テーブル、データ制約 |
| [architecture-react-dotnet.md](docs/architecture-react-dotnet.md) | React・ASP.NET Core の責務、HTTP JSON 契約、起動単位、並列 E2E |
| [project.md](docs/project.md) | 目的、制約、外部事実、認証・配備・DB 運用、実行手順 |
| [document-policy.md](docs/document-policy.md) | 適用する標準、固有差分、正本の責務 |

サンプルの UI、タイトル一意制約、文字数制限は参照用仕様です。製品開発時は `backend/Features/Notes/`、`frontend/src/usecases/`、E2E を製品のユースケースに置き換えます。テンプレート名が残る `package.json`、`frontend/index.html`、`frontend/src/app/Shell.tsx`、`.github/workflows/react-dotnet-template.yml` も製品名へ置き換えます。

サンプルの仕様は製品の承認済み仕様として扱いません。導入時は [導入開始手順](https://github.com/nuitsjp/aidd-project-template#3-初期セットアップと最初のユースケース) を確認し、製品固有の仕様を定義します。

共通資材の更新対象と固有資材の管理範囲は [文書方針](docs/document-policy.md#adoption) に従います。

`.github/workflows/react-dotnet-template.yml` は生成プロジェクト用の CI 例です（生成後のルートで実行することを前提としています）。
