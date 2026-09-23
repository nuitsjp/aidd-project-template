# React + .NET テンプレート

新規プロジェクトは配布元ルートの `mise run init:react-dotnet <新しい出力先> --name Company.Product` で生成します。`--name` は ASCII の C# 識別子をドットで区切った製品名です。生成先ルートには React・ASP.NET Core・SQLite の実行可能なアプリ一式が入り、C# 名前空間、ソリューション・プロジェクト・アセンブリ名と npm パッケージ名が指定した製品名に合わせられます。メモの参照実装は `NotesSample` のまま `reference/` に残します。製品の現行仕様はルートの `docs/` に記述し、メモのユースケース・シナリオと設計は `reference/docs/` に保持します。

生成先ルートで `mise trust`、`mise run setup`、`mise run setup:browser` を実行し、`mise run dev` で製品アプリを起動するか `mise run verify` で検証します。`mise run build` は UI を同梱した .NET 配布物を作成します。製品ルートには `<製品名>.slnx`、`backend/<製品名>.csproj`、`frontend/<製品名>.Frontend.esproj` と、`tests/backend/` の既存テスト、`tests/dotnet-unit/` の単体テスト、`tests/dotnet-integration/` の統合テストの各プロジェクトが入ります。Visual Studio ではルートの `<製品名>.slnx` を開き、`backend/<製品名>.csproj` をスタートアッププロジェクトにして F5 で起動します。サンプルは `reference/` へ移動して独立した `mise.toml` のタスクを実行し、Visual Studio では `reference/App.slnx` を開きます。

サンプルの構成、実行手順、ユースケースは [reference/README.md](reference/README.md) から参照してください。共通の `AGENTS.md`、標準、文書検査スクリプトは生成時に `template/` からルートへ配置します。生成時に依存取得やコード生成は行わず、既存の出力先は上書きしません。配布元の `react-dotnet-template/` では製品文書を検査し、アプリの実行・検証は `react-dotnet-template/reference/` から行います。
