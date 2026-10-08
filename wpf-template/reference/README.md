# WPF メモアプリの参照実装

XAML の対話から .NET Framework 4.8.1・SQLite の実処理までを通す参照アプリです。`View/`・`ViewModel/`・`Model/` を持つ単一の WPF プロジェクトで、Kamishibai が画面遷移と起動時の組み立て、CommunityToolkit.Mvvm が通知とコマンドを担当します。名前空間・アセンブリ・アプリ ID は `WpfNotesSample`、ソースのプロジェクトファイルは `app/App.csproj` です。

Windows でこのディレクトリの `mise trust`、`mise run setup`、`mise run dev` を実行します。検証は `mise run verify`、画面確認用のモックは `mise run dev:mock` です。Visual Studio ではこのディレクトリのソリューションを開き、`app/App.csproj` をスタートアップにして F5 で起動します。詳しい前提と操作は [実行・検証手順](docs/project.md#commands) に記載します。

仕様は [ユースケース一覧](docs/project.md#usecases)、設計は [全体構成](docs/architecture.md)・[WPF 固有設計](docs/architecture-wpf.md)・[データ設計](docs/design/data.md) を参照してください。生成時はアプリを製品ルートへコピーし、製品側の名前だけを変更します。この `reference/` はサンプルとして残り、メモの仕様を製品の合意済み仕様として扱いません。
