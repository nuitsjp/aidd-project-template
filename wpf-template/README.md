# WPF テンプレート

新規プロジェクトは配布元ルートの `mise run init:wpf <新しい出力先> --name Company.Product` で生成します。製品ルートに .NET Framework 4.8.1 の WPF アプリを配置し、C# 名前空間・プロジェクト・アセンブリ・アプリ ID を指定した製品名へ変更します。元の `WpfNotesSample` は `reference/` に残します。製品仕様はルートの共通 `docs/` で管理します。

Windows で生成先ルートの `mise trust`、`mise run setup` を実行し、`mise run dev` で起動するか `mise run verify` で検証します。Visual Studio ではルートのソリューションを開き、`app/<製品名>.csproj` をスタートアップにして F5 で起動します。必要な開発環境、各タスク、配布方法は [参照アプリのプロジェクト定義](reference/docs/project.md#commands) を参照してください。

配布元のこのディレクトリはツール導入と製品文書検査を担当し、アプリの開発・検証は `reference/` で行います。[reference/README.md](reference/README.md) に参照実装の案内があります。共通規則と文書検査資材は生成時に `template/` から配置し、依存取得・ビルドは行いません。既存の出力先は拒否します。
