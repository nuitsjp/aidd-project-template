# WPF Template のプロジェクト定義

## 1. 目的と範囲

Windows のネイティブな WPF 画面から実 SQLite の確定までを通すメモアプリです。単票の作成・編集・削除と、複数メモの入力・確認・一括登録を示します。参照実装は `reference/` に残し、生成先ルートでは製品固有の仕様を管理します。

<a id="constraints"></a>
## 2. 制約・品質要求・受け入れ条件

.NET Framework 4.8.1 に対応する Windows の x64 環境で、ネイティブな単一の WPF プロセスを使用します。通常起動では同じアプリ ID とデータ領域を 1 インスタンスで使用し、通常画面は 1 ウィンドウです。メモは Windows 利用者のローカルデータとして扱います。

編集内容は保存まで下書きとして保持し、失敗や未保存の離脱取り消しで消失させません。保存・削除の版検査と一括登録の原子性、入力値の制約、保存場所は [データ設計](design/data.md) に従います。画面の確認待ちは DB トランザクションに含めません。

<a id="usecases"></a>
## 3. ユースケース一覧

| ユースケース | 主アクター | 目的 | 実装順序 | 実現パターン | モック適用 |
| --- | --- | --- | --- | --- | --- |
| [メモを作成・編集して保存する](usecases/メモを作成・編集して保存する/README.md) | 利用者 | メモを保存し、参照・編集・削除する | 1 | [UCP-1](design/UCP-1.md) | 対象 |
| [複数のメモを確認して一括登録する](usecases/複数のメモを確認して一括登録する/README.md) | 利用者 | 入力した複数メモを確認して一体で保存する | 2 | [UCP-2](design/UCP-2.md) | 対象 |

<a id="design"></a>
## 4. 確認した事実

実装の直接依存は `app/App.csproj`、テストの依存は各テストプロジェクト、実行タスクはこのディレクトリの `mise.toml` を正本とします。C# は `latest`、ターゲットは `net481` です。開発用 .NET SDK は `global.json` の 10.0.100 以上を `latestFeature` で選び、mise では 10.0 系を導入します。SDK の版と配布先の Framework ランタイムは別です。

- **Kamishibai 4.0.0**: 公式の対応範囲は Framework 4.6.2 以上です。`Kamishibai.Hosting` を通じて Generic Host と画面遷移を利用します（[公式の導入説明](https://github.com/nuitsjp/KAMISHIBAI/blob/master/docs/02-getting-started.md)）。
- **CommunityToolkit.Mvvm 8.4.2**: .NET Standard 2.0 の資材を持ち、通知とコマンドを実装します（[公式 NuGet](https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2)）。
- **テスト**: xUnit.v3 4.0.1、Shouldly 4.3.0 と Friendly を使用します。UI 操作には RM.Friendly.WPFStandardControls 1.63.0、ネイティブの確認ダイアログ操作には Codeer.Friendly.Windows.NativeStandardControls 2.20.0 を使用します。テスト範囲は [WPF 固有設計](architecture-wpf.md#test-boundary) に記載します。

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
| `mise run setup` | SDK・Python の導入と NuGet ロックに従う依存復元 |
| `mise run dev` | 実 SQLite を使う Debug アプリを起動 |
| `mise run dev:mock` | Mock 構成をビルドし、`--mock` で固定データの対話を起動 |
| `mise run build` | Release の実処理アプリをビルド |
| `mise run test:unit` | 規則・共通処理・ViewModel の単体テスト |
| `mise run test:integration` | 専用の実 SQLite ファイルを使う統合テスト |
| `mise run test:e2e` | Friendly で実アプリを操作し、別接続から保存内容を確認 |
| `mise run check:docs` | 製品文書と参照文書のリンク・構造・共通規則を検査 |
| `mise run verify` | 文書、ビルド、単体、統合、UI テストを検証 |
| `mise run package` | 検証後に exe・DLL・config・SQLite の実行資材・LICENSE を ZIP 化 |

DBML の構文検証は Node.js のある環境で、次の独立したコマンドを実行します（[公式 CLI](https://dbml.dbdiagram.io/cli/)）。製品アプリのビルド・テストは .NET SDK で実行します。Node.js はテンプレート生成とこの DBML 検査に使用します。

```powershell
npx --yes --package=@dbml/cli@9.1.1 dbml2sql docs/design/data.dbml --postgres
```

この SQL 出力はローカルの構文検査用で、SQLite の移行スクリプトには使用しません。

通常は利用者の LocalAppData にアプリ ID ごとのデータを保存します。`--data-dir <ディレクトリ>` は保存先とインスタンスの分離に使います。UI テストは専用の一時ディレクトリを指定し、普段のメモを操作しません。

モックと実処理の切替は [モック境界](architecture-wpf.md#mock-boundary) に従います。通常起動は実処理です。モックを閉じて `mise run dev` を実行すると実 DB を使います。Debug / Release では `--mock` を拒否し、接続・保存失敗からモックへ切り替えません。

Visual Studio では `app/App.csproj` をスタートアップにし、Debug / Release で実処理を起動します。モックは `mise run dev:mock` で起動します。画面の終了操作で終了し、未保存時は確認ダイアログで終了を取り消せます。

参照アプリのビルド先は `app/bin/<構成>/net481/`、実行ファイルは `WpfNotesSample.exe` です。`package` は `release/app/` に実行資材を配置し、同ディレクトリを含む `release/WpfNotesSample.zip` を作成します。生成した製品では exe と ZIP の名前を製品名へ変更します。既存の `release/` は上書きせず、内容を確認して移動してから再作成します。

DB は配布 ZIP に含めません。ZIP の展開先の `app/` にある exe を、同梱 DLL・config・LICENSE とともに使用します。

自動検証は受け入れ条件の保存結果・失敗時状態と UI 操作を対象にします。DPI 100% / 150% / 200% での欠け、キーボード操作、日本語 IME の確定・未確定入力、未保存の画面離脱と終了は Windows 実機で確認します。これらの実機確認は未検証です。
