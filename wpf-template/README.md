# WPF テンプレート

新規プロジェクトは配布元ルートの `mise run init:wpf <新しい出力先> --name Company.Product` で生成します。製品ルートに .NET Framework 4.8.1 の WPF アプリを配置し、C# 名前空間・プロジェクト・アセンブリ・アプリ ID を指定した製品名へ変更します。一覧・詳細の表示とノートの追加・更新・削除を示す元の `WpfNotesSample` は `reference/` に残します。製品仕様はルートの共通 `docs/` で管理します。

Windows で生成先ルートの `mise trust`、`mise run setup` を実行し、`mise run dev` で起動するか `mise run verify` で検証します。Visual Studio ではルートのソリューションを開き、`app/<製品名>.csproj` をスタートアップにして F5 で起動します。必要な開発環境、各タスク、配布方法は [参照アプリのプロジェクト定義](reference/docs/project.md#commands) を参照してください。

生成する画面は [Material Design 3](https://m3.material.io/) に準拠したネイティブ WPF の共通テーマを使い、ライトとダークをタイトルバーで切り替えます。テーマとスタイルは `app/View/Styles/` に集約し、PowerShell の `mise run check:xaml` で共通領域外の Style・ControlTemplate 定義を検出します。この検査と検査スクリプトのテストは `verify` に含まれます。

配布元のこのディレクトリはツール導入と製品文書検査を担当し、アプリの開発・検証は `reference/` で行います。[reference/README.md](reference/README.md) に参照実装の案内があります。新規プロジェクトはこの案内の `init:wpf` を必ず使い、既存リポジトリへ導入する場合も空の一時出力へ生成してから、生成された製品ルートの必要な全構成を移します。`reference/` だけをコピーして製品コードを新規実装する手順は使いません。生成後は、`<製品名>.slnx`、`app/<製品名>.csproj`、テスト、共通文書・検査資材、`LICENSE` が製品ルートにそろい、`reference/App.slnx` と参照アプリの元の名前・内容が残っていること、製品側の名前置換と生成物の除外が行われていることを確認します。共通規則と文書検査資材は生成時に `template/` から配置し、依存取得・ビルドは行いません。既存の出力先は拒否します。生成先で `python scripts/doc_check.py .` と `mise run verify` を実行してから開発を始めます。
