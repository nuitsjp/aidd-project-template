# React + .NET テンプレート

新規プロジェクトは配布元ルートの `mise run init:react-dotnet <出力先>` で生成します。生成先の `reference/` には、メモのユースケース・シナリオ、設計文書、React・ASP.NET Core・SQLite の実装、テストを実行可能な一式として残します。これらは製品の確定仕様ではありません。製品の現行仕様は生成先ルートの `docs/` に記述します。

ルートの mise タスクは現在、`reference/` の実行と検証を対象とします。初回はルートで `mise trust`、`mise run setup`、`mise run setup:browser` を実行します。開発は `mise run dev`、全検証は `mise run verify` です。Visual Studio では `reference/App.slnx` を開き、`reference/backend/App.csproj` をスタートアッププロジェクトにします。

サンプルの構成、実行手順、ユースケースは [reference/README.md](reference/README.md) から参照してください。共通の `AGENTS.md`、標準、文書検査スクリプトは生成時に `template/` からルートへ配置します。テンプレート開発時にもこのディレクトリから同じ mise タスクを実行できます。
