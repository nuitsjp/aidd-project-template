# React + .NET テンプレート

新規プロジェクトは配布元ルートの `mise run init:react-dotnet <出力先>` で生成します。生成先の `reference/` には、メモのユースケース・シナリオ、設計文書、React・ASP.NET Core・SQLite の実装、テストを実行可能な一式として残します。これらは製品の確定仕様ではありません。製品の現行仕様は生成先ルートの `docs/` に記述します。

ルートの `mise.toml` は実プロジェクト用です。現時点では固定版ツールの導入（`mise run setup`）と製品文書の検査（`mise run check:docs`）を定義し、製品の実装に合わせてアプリ用タスクを追加します。サンプルは `reference/mise.toml` で独立して管理します。`reference/` に移動し、`mise trust`、`mise run setup`、`mise run setup:browser` の後に `mise run dev` または `mise run verify` を実行します。Visual Studio では `reference/App.slnx` を開き、`reference/backend/App.csproj` をスタートアッププロジェクトにします。

サンプルの構成、実行手順、ユースケースは [reference/README.md](reference/README.md) から参照してください。共通の `AGENTS.md`、標準、文書検査スクリプトは生成時に `template/` からルートへ配置します。テンプレート開発時も `react-dotnet-template/reference/` からサンプルの mise タスクを実行できます。
