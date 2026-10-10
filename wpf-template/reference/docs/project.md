# WPF Template のプロジェクト定義

## 1. 目的と範囲

Windows のネイティブな WPF 画面から実 SQLite の確定までを通すノートアプリです。一覧・詳細の表示と、ノートの追加・更新・削除を示します。参照実装は `reference/` に残し、生成先ルートでは製品固有の仕様を管理します。

<a id="constraints"></a>
## 2. 制約・品質要求・受け入れ条件

.NET Framework 4.8.1 に対応する Windows の x64 環境で、ネイティブな単一の WPF プロセスを使用します。通常起動では同じアプリ ID とデータ領域を 1 インスタンスで使用し、通常画面は 1 ウィンドウです。ノートは Windows 利用者のローカルデータとして扱います。

編集内容は保存まで下書きとして保持し、失敗や未保存の離脱取り消しで消失させません。保存・削除の版検査、入力値の制約、保存場所は [データ設計](design/data.md) に従います。画面の確認待ちは DB トランザクションに含めません。

画面は Ant Design 6.6.5 の既定のライトテーマに準拠し、WPF の共通テーマ・標準スタイル・用途別スタイルとアプリ側で描くウィンドウ枠で構成します。画面内で Style・ControlTemplate を定義せず、`app/View/Styles/` の定義を使います。適用範囲と構成は [WPF 固有設計](architecture-wpf.md#style-boundary)、定義場所の検査は [XAML の静的検査](architecture-wpf.md#xaml-boundary) に従います。

<a id="usecases"></a>
## 3. ユースケース一覧

| ユースケース | 主アクター | 目的 | 実装順序 | 実現パターン | モック適用 |
| --- | --- | --- | --- | --- | --- |
| [ノート一覧の表示](usecases/ノート一覧の表示/README.md) | 利用者 | 保存済みのタイトルと更新日時を確認する | 1 | [UCP-1](design/UCP-1.md) | 対象 |
| [ノート詳細の表示](usecases/ノート詳細の表示/README.md) | 利用者 | 選択した最新のタイトルと本文を確認する | 2 | [UCP-1](design/UCP-1.md) | 対象 |
| [ノートの追加](usecases/ノートの追加/README.md) | 利用者 | 新しいノートを保存する | 3 | [UCP-1](design/UCP-1.md) | 対象 |
| [ノートの更新](usecases/ノートの更新/README.md) | 利用者 | 保存済みのノートを変更する | 4 | [UCP-1](design/UCP-1.md) | 対象 |
| [ノートの削除](usecases/ノートの削除/README.md) | 利用者 | 表示中のノートだけを削除する | 5 | [UCP-1](design/UCP-1.md) | 対象 |

<a id="design"></a>
## 4. 確認した事実

実装の直接依存は `app/App.csproj`、テストの依存は各テストプロジェクト、実行タスクはこのディレクトリの `mise.toml` を正本とします。C# は `latest`、ターゲットは `net481` です。開発用 .NET SDK は `global.json` の 10.0.100 以上を `latestFeature` で選び、mise では 10.0 系を導入します。SDK の版と配布先の Framework ランタイムは別です。

- **Kamishibai 4.0.0**: 公式の対応範囲は Framework 4.6.2 以上です。`Kamishibai.Hosting` を通じて Generic Host と画面遷移を利用します（[公式の導入説明](https://github.com/nuitsjp/KAMISHIBAI/blob/master/docs/02-getting-started.md)）。
- **CommunityToolkit.Mvvm 8.4.2**: .NET Standard 2.0 の資材を持ち、通知とコマンドを実装します（[公式 NuGet](https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2)）。
- **静的解析と整形**: .NET SDK 同梱のアナライザー（`latest-recommended`）、WpfAnalyzers 4.1.1、SonarAnalyzer.CSharp 10.35.0.4138、`dotnet format`、XamlStyler.Console 3.2501.8 を使用します。SonarAnalyzer は Sonar Source-Available License v1.0 で、開発時のみ使用し配布物に含めません。XamlStyler は .NET 8 向けのツールのため、ツールマニフェストの `rollForward` で .NET 10 上で実行します。規約と構成は [静的解析と整形](architecture-wpf.md#code-quality) に記載します。
- **テスト**: xUnit.v3 4.0.1、Shouldly 4.3.0 と Friendly を使用します。網羅率の計測には dotnet-coverage 18.12.0 を使用します。UI 操作には RM.Friendly.WPFStandardControls 1.63.0 を使用します。テスト範囲は [WPF 固有設計](architecture-wpf.md#test-boundary) に記載します。

<a id="commands"></a>
## 5. 実行・切り替え・検証手順

配布元では `wpf-template/reference/`、採用先では生成先ルート、参照サンプルを使う場合はその `reference/` を作業ディレクトリにします。以下の相対パスはアプリの作業ディレクトリを起点とします。Windows PowerShell 5.1 で `scripts/run.ps1` がタスクを実行します。

Visual Studio または Build Tools の .NET デスクトップ開発環境と .NET Framework 4.8.1 Developer Pack を用意します。配布先には Framework 4.8.1 のランタイムが必要です。初回は `mise.toml` を確認して信頼し、ツールと NuGet 依存を取得します。

```powershell
mise trust
mise run setup
mise run dev
```

| タスク | 内容 |
| --- | --- |
| `mise run setup` | SDK・Python の導入、NuGet ロックに従う依存復元、ローカルツールの復元 |
| `mise run dev` | 開発用の初期データ `dev-data/notes.db` で Debug アプリを起動 |
| `mise run dev:mock` | Mock 構成をビルドし、`--mock` で固定データの対話を起動 |
| `mise run build` | Release の実処理アプリをビルドし、層の依存違反を検査 |
| `mise run format` | C# を dotnet format、XAML を XamlStyler で整形 |
| `mise run check:format` | 変更せずに C#・XAML の整形漏れとコード規約の違反を検査 |
| `mise run test:unit` | 単体テストを実行し、製品の行網羅率を単独レポートへ保存（閾値判定なし） |
| `mise run test:integration` | 実 SQLite を使う統合テストを実行し、製品の行網羅率を単独レポートへ保存（閾値判定なし） |
| `mise run test:e2e` | Friendly で実アプリを操作し、子アプリを含む製品の行網羅率を単独レポートへ保存（閾値判定なし） |
| `mise run check:docs` | 製品文書と参照文書のリンク・構造・共通規則を検査 |
| `mise run check:xaml` | PowerShell で共通領域外の Style・ControlTemplate 定義と XML 構文を検査 |
| `mise run test:xaml` | 検査用 XAML に対する許可・違反・解析失敗の診断を確認 |
| `mise run verify` | 文書・整形・コード規約・XAML を順に検査し、検査スクリプトの回帰検証と並走して層の依存検査を含む Release・Mock ビルドを実行後、アナライザーの回帰を含む単体・統合・E2E を並列実行して統合した行網羅率を判定 |
| `mise run package` | 検証後に exe・DLL・config・SQLite の実行資材・LICENSE・第三者ライセンスを ZIP 化 |

網羅率の出力先は実行ごとの `TestResults/<UUID>/coverage.cobertura.xml` です。`verify` は全テストのヒットを合算し、製品全体の網羅率を表示したうえで Domain・ViewModel の 75%、Infrastructure の 95% を判定します。グループの失敗があっても、全グループの終了とレポート保存を待って失敗します。計測範囲とテスト追加の方針は [テストの分担](architecture-wpf.md#test-boundary) に従います。

DBML の構文検証は Node.js のある環境で、次の独立したコマンドを実行します（[公式 CLI](https://dbml.dbdiagram.io/cli/)）。製品アプリのビルド・テストは .NET SDK で実行します。Node.js はテンプレート生成とこの DBML 検査に使用します。

```powershell
npx --yes --package=@dbml/cli@9.1.1 dbml2sql docs/design/data.dbml --postgres
```

この SQL 出力はローカルの構文検査用で、SQLite の移行スクリプトには使用しません。

通常は利用者の LocalAppData にアプリ ID ごとのデータを保存します。`--data-dir <ディレクトリ>` は保存先とインスタンスの分離に使います。開発時の起動（`mise run dev` と Visual Studio の F5）は `dev-data/` を指定し、リポジトリで管理する初期データのノートを直接操作します。初期状態へ戻すときは `git restore dev-data/notes.db` を実行します。UI テストは専用の一時ディレクトリを指定し、普段のノートや初期データを操作しません。

モックと実処理の切替は [モック境界](architecture-wpf.md#mock-boundary) に従います。通常起動は実処理です。モックを閉じて `mise run dev` を実行すると実 DB を使います。Debug / Release では `--mock` を拒否し、接続・保存失敗からモックへ切り替えません。

Visual Studio では `app/App.csproj` をスタートアップにし、Debug / Release で実処理を起動します。起動プロファイル `app/Properties/launchSettings.json` が `--data-dir` に `dev-data/` を渡します。モックは `mise run dev:mock` で起動します。画面の終了操作で終了し、未保存時は確認ダイアログで終了を取り消せます。

参照アプリのビルド先は `app/bin/<構成>/net481/`、実行ファイルは `WpfNotesSample.exe` です。`package` は `release/app/` に実行資材を配置し、同ディレクトリを含む `release/WpfNotesSample.zip` を作成します。生成した製品では exe と ZIP の名前を製品名へ変更します。既存の `release/` は上書きせず、内容を確認して移動してから再作成します。

DB は配布 ZIP に含めません。ZIP の展開先の `app/` にある exe を、同梱 DLL・config・LICENSE・`THIRD-PARTY-NOTICES.txt` とともに使用します。

自動検証は一覧・詳細の取得、保存結果、失敗時状態、未保存の画面離脱・終了確認を対象にします。DPI 100% / 150% / 200% での欠け、キーボード操作、日本語 IME の確定・未確定入力は Windows 実機で確認します。これらの実機確認は未検証です。

`package` による ZIP 作成、実 DB・モック実装を含まないこと、同じ開発端末での展開後の起動・追加・再起動後の保存値は確認済みです。開発資材を導入していない別端末での起動は未検証です。
