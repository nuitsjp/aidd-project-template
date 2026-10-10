# WPF アーキテクチャ

本書の実装パスは `reference/` を起点とします。製品の設計は生成先ルートの `docs/` で管理します。

## 1. 起動と画面

`app/App.csproj` は `net481`・C# `latest` の単一 WPF プロジェクトです。起動処理は Kamishibai の Generic Host で Domain の契約に対応する Infrastructure と View / ViewModel を登録します。アプリ ID・データ領域・実処理またはモックのモードに対応するインスタンス判定を行い、同じ対象の 2 回目の起動は新しい通常ウィンドウを開きません。多重起動と起動失敗の通知はメインウィンドウの表示前に行うため、OS 標準の MessageBox を使います。

一覧、詳細、追加・更新で共有する編集画面を `NavigationFrame` 内で切り替えます。ウィンドウ枠は OS のタイトルバーを使わず、`WindowChrome` でアプリ側に描きます。タイトルバーにはアプリの印・アプリ名・モック時のタグと、テーマ切り替え・最小化・最大化・閉じる操作を置きます。移動・ダブルクリックでの最大化・端でのサイズ変更は OS の機能を使います。最大化ボタンは自前のため、Windows 11 のスナップレイアウトはボタンに重ねても表示されません。画面の中身は各画面の上部 1 行に操作をまとめ、一覧は上部に再取得、一覧の色面の右下に追加、詳細・編集は左端に一覧へ戻る操作を置きます。Kamishibai の `IPresentationService` が遷移を担当し、`AddPresentation` で View と ViewModel を対応付けます。確認は ViewModel が `IDialogService` に依頼し、View 側の `DialogService` が確認ダイアログを表示します。CommunityToolkit.Mvvm の通知と Command を使います。画面の XAML に SQL や業務確定を置きません。

<a id="style-boundary"></a>
### 共通テーマとスタイル

[Material Design 3](https://m3.material.io/) の役割トークン・タイプスケール・形状スケールに準拠し、コンポーネントの見た目をネイティブ WPF で再現します。Material Components のライブラリや API との互換性はありません。配色はソースカラー #0F6CBD から作ったトーナルパレットで、ライトとダークの 2 組を持ちます。

`App.xaml` は次の順に ResourceDictionary を読み込みます。

| ファイル | 保持するもの |
| --- | --- |
| `View/Styles/Colors.Light.xaml` | ライトの色の役割トークンと、テーマ切り替えボタンのアイコン・文言 |
| `View/Styles/Theme.xaml` | 色以外のトークン（書体、文字サイズと行高、コントロールの高さ、角丸、余白） |
| `View/Styles/Icons.xaml` | Ant Design Icons の形状（`@ant-design/icons-svg` 4.6.0、MIT License。全文は `THIRD-PARTY-NOTICES.txt`）と、このアプリで描いたノート・時計の形 |
| `View/Styles/Controls.xaml` | アイコン表示と Button（outlined）・入力欄の本体・ラベル・Tooltip・List・スクロールバーの標準スタイル |
| `View/Styles/Roles.xaml` | Button の filled・tonal・text・error の outlined・icon と extended FAB、見出し、色面、Banner、タグ、一覧の項目、空表示、filled text field、dialog |
| `View/Styles/Window.xaml` | タイトルバーを含むウィンドウ枠と確認ダイアログの器。操作ボタンとテーマ切り替えの処理は同名の `.xaml.cs` に置く |

`View/Styles/Colors.Dark.xaml` は `Colors.Light.xaml` と同じキーを持つダークの配色です。色は画面とスタイルから `DynamicResource` で参照し、タイトルバーのテーマ切り替えで先頭の配色辞書を差し替えて、全画面と確認ダイアログに反映します。起動時はライトで、選んだテーマは保存しません。色以外の値は `StaticResource` で参照します。

ボタン・アイコン・入力欄は Material Design 3 の値に合わせます。
- **ボタン**: 高さ 40 の全丸で、アイコンの左に 16、文字の右に 24 の余白を置きます（text は左右 12）。hover と pressed は内容色の層（8% と 10%）、フォーカスは外側の 2 の線で示します。無効時は容器を on-surface の 12%、内容を 38% にします。
- **アイコン**: 操作は文字で示し、必要に応じてアイコンを左に添えます（間隔 8）。アイコンのみのボタンは、戻る操作とタイトルバーの操作に限ります。大きさはボタンで 18、戻る操作と追加で 24 です。形は `View/Icon.cs` の添付プロパティ `Icon.Data` で指定します。
- **入力欄**: filled text field です。上側だけ角丸 4 の色面にラベルと入力を重ね、下辺の線をフォーカス時に 1 から 3 へ太くして primary で示します。制約は欄の下の補足文で示します。

ページの背景は surface とし、一覧と詳細の本文を surface container lowest の色面（角丸 28）に載せて境界を示します。一覧は List の 2 行の項目で、タイトルの頭文字の円、タイトル、更新日時、詳細の text ボタンを並べ、選択中の項目は secondary container の色面で示します。追加は一覧の色面の右下に重ねた extended FAB です。空のときは傾けた角丸の色面にノートの形と次の操作を表示します。エラーは error container、保存結果は secondary container の Banner、モック表示は tertiary container のタグで示します。確認ダイアログは basic dialog と同じく、中央揃えのアイコンと見出し、本文、右寄せの text ボタン（キャンセルと OK）を並べ、親ウィンドウの中央に表示します。表示中は親ウィンドウを scrim で覆います。

書体は欧文の Roboto と日本語の Noto Sans JP で、Regular（400）と Medium（500）の TrueType を `app/Fonts/` に置き、WPF の Resource として実行ファイルに埋め込みます。ボタン・ラベル・見出しの強調は Medium です。どちらも SIL Open Font License 1.1 で、表示と全文は `THIRD-PARTY-NOTICES.txt` に置きます。ウィンドウと確認ダイアログの角丸・影は Windows 11 の OS（DWM）が描くため、dialog の角丸は Material Design 3 の 28 ではなく OS の値になります。

通常のコントロールは型に対応する暗黙のスタイルを使い、用途による違いは共通の名前付きスタイルを参照します。各画面には配置、Binding、入力動作と共通スタイルへの参照を置き、Style・ControlTemplate の定義は `View/Styles/` に集約します。

<a id="state-domain"></a>
## 2. 状態の分担と Domain

| 領域 | 保持するもの・責務 |
| --- | --- |
| `View/` | XAML、Binding、フォーカス・スクロール等の表示・入力状態、ウィンドウ枠、確認ダイアログ |
| `ViewModel/` | 下書き、表示データ、実行・エラー状態、`NavigationState` の現在ページ、確認の依頼（`IDialogService`） |
| `Domain/Notes/` | 不変なノート・入力値、入力規則と一覧・詳細取得・保存・削除の契約 |
| `Infrastructure/Sqlite/` | SQLite の接続・初期化・SQL 移行 |
| `Infrastructure/Sqlite/Notes/` | 通常の永続状態を SQLite で保持する `NotesService` |
| `Infrastructure/InMemory/Notes/` | Mock 構成だけで状態を保持する `InMemoryNotesService` |
| `Compatibility/` | .NET Framework 向けのコンパイラ支援 `IsExternalInit` |

4 領域間の依存方向は View → ViewModel → Domain ← Infrastructure です。View は ViewModel、ViewModel と Infrastructure は Domain を参照し、Domain はこの 4 領域の他層を参照しません。起動処理の `App` は DI の合成点として全層を参照できます。自動検査の対象範囲と診断は [層の依存検査](#layer-analysis) に従います。

各領域は `app/` 直下に配置します。今回は機能の組合せが不要なので、ViewModel が Domain の `INotesService` を直接利用します。Infrastructure は技術別に配置し、その下を業務領域別に分けます。`Database.cs` と `Migrations/` は Sqlite 直下に置きます。モックは起動時に同じ契約へ差し替え、通常構成で実処理の失敗を補いません。

Domain は変更可能な業務状態を保持しません。参照型は `sealed record`、値型は `readonly record struct` とし、`enum`・`interface`・純粋関数の `static class` を基本とします。業務例外は `Exception` を継承する `sealed class` を許容します。record 内部の値と参照先も不変とし、可変な static フィールドやキャッシュを置きません。

検証・計算・正規化は同じ入力から同じ結果を返します。時刻・乱数・外部から得た値が必要なら引数で受け取り、Domain に DB・ファイル・UI 等の I/O 処理を置きません。`Note` と `NoteInput` は `sealed` な positional record、`NoteRules` は純粋関数を持つ `static class` です。`IsExternalInit` は Domain の型ではなく、`app/Compatibility/` のコンパイラ支援です。これらはコードレビューで確認する基準とします。

保存中は重複実行・離脱・終了を受け付けません。下書きは保存・破棄の完了まで ViewModel に保持し、保存失敗で置き換えません。未保存の画面離脱・終了では確認を求め、取り消し時は現在の画面と入力を維持します。

入力境界で値を検証し、版競合・対象なし・保存失敗を対話で表示します。保存時は返された確定値を編集画面に反映して保存完了を表示し、後続の一覧取得失敗で保存成功を変更しません。

## 3. 一覧と詳細

一覧は起動時と一覧への移動時に取得し、タイトル・更新日時を表示します。更新日時の降順、同じ日時では ID 順です。画面上部に再取得、一覧の右下に追加、各項目に詳細表示の操作を持ちます。再取得は取得に成功してから一覧を置き換え、失敗時は表示中の一覧を維持してエラーを表示します。

詳細は選択した ID で DB の最新のノートを再取得し、タイトル・更新日時・本文を読み取り専用で表示します。本文は画面の残りの高さを使います。対象なしや取得失敗を明示し、編集・削除不可とします。取得できた詳細からは、アイコン付きの「削除」「編集」ボタンで共有の編集画面への移動と削除を行い、左端の戻る操作で一覧へ戻ります。保存後は編集画面に確定値と成功表示を維持し、一覧へ戻る操作で再取得します。

<a id="mock-boundary"></a>
## 4. モック境界

起動時の DI の組み立てで、ViewModel が利用する Domain のサービス契約の実装を実 SQLite または固定データの実装へ切り替えます。View と ViewModel、入出力型は両方で共有します。`dev:mock` は Mock ビルドと明示的な `--mock` を使い、画面にモックであることを表示します。

固定データの実装は Mock 構成にだけ含めます。Debug / Release のアセンブリと配布 ZIP に固定データを含めず、実処理の失敗をモックで補いません。

<a id="test-boundary"></a>
## 5. テストの分担

単体テストは入力規則、ViewModel の下書き・実行状態・取得失敗を対象にします。統合テストは実 SQLite を通し、最新取得・保存・削除の版検査とロールバックを確認します。各テストに専用の一時 DB を用意します。

UI テストは Friendly の `WindowsAppFriend` で実アプリに接続します。`Drivers/` にメインウィンドウ、一覧、詳細、編集、確認ダイアログの専用ドライバーを置き、シナリオは型付き操作と表示値で検証します。WPF の部品は XAML の `x:Name` で宣言したフィールドを `dynamic` から参照し、画面は `VisualTree().ByType<画面型>()` で取得します。確認ダイアログは `ConfirmDialog` の `x:Name` で操作します。

`verify` は文書・整形・コード規約・XAML の検査を順に実行した後、XAML・coverage・runner の回帰検証を独立した PowerShell プロセスで並列に起動します。回帰検証は製品の Release・Mock ビルドと並走し、ビルド後に単体・統合・E2E の 3 グループを並列に実行します。dotnet-coverage 18.12.0 の同一 collect セッションで、Friendly が起動する子アプリも含め、製品の全 C# 実行可能行のヒットを合算します。Mock 構成、`obj/` 配下、`[GeneratedCode]` は計測から除きます。`[CompilerGenerated]` は除外せず、自動プロパティも対象です。

統合レポートを実行ごとの `TestResults/<UUID>/coverage.cobertura.xml` に保存し、製品全体の行網羅率を表示します。閾値は全テストを合算した `Domain/` と `ViewModel/` の 75%、`Infrastructure/` の 95% で、テスト種類ごとには判定しません。個別の `test:*` も同じ方式で単独レポートを保存しますが、閾値は判定しません。網羅率を上げることだけを目的にテストを追加しません。

回帰ログは網羅率の計測とは別の UUID による `TestResults/<UUID>/` に保存します。各グループの `xaml-checks.stdout.log`、`coverage-checks.stdout.log`、`runner-checks.stdout.log` と対応する `.stderr.log`、親の集約ログ `checks.stdout.log`・`checks.stderr.log` を出力します。テストと回帰検証の全グループの終了、レポートとログの保存を待って結果を判定し、回帰検証だけが失敗した場合も `verify` と `package` は失敗します。

`Support/AppSession` は起動・終了と `--data-dir` による一時 DB の分離、`NoteDatabase` は別接続による保存結果の検証を担当します。各ケースに専用のアプリプロセスと一時 DB を用意します。5 ユースケースとモック構成の起動確認は、テストクラスごとに xUnit の collection を構成し、collection 間を最大 2 並列で実行します。同一 collection 内のケースは直列に実行し、シナリオ内の手順順序を維持します。終了後に一時データを片付けます。

5 ユースケースの UI テストは `tests/e2e/<ユースケース名>/<同名シナリオ>.cs` に配置し、開始条件、手順、受け入れ条件を文書と同じ番号で区切ります。エラー・競合・未保存確認・再起動の条件違いも該当するシナリオ内で検証します。モック構成の起動・再起動確認は `tests/e2e/Runtime/` に分離します。実行コマンドと実機確認の条件は [プロジェクト定義](project.md#commands) に記載します。

<a id="xaml-boundary"></a>
### XAML の静的検査

`scripts/check-xaml.ps1` は Windows PowerShell 5.1 と .NET の `XmlReader`・`IXmlLineInfo` を使い、追加パッケージなしで XAML の定義場所と XML 構文を検査します。`-AppRoot` の既定値は `app` です。その配下の XAML を名前空間付きで解析し、`View/Styles/` 以外にある WPF の Style を `WPFXAML001`、ControlTemplate を `WPFXAML002`、不正な XML を `WPFXAML003` として報告します。共通スタイルへの参照は許可します。

診断形式は `ファイル(行,列): error コード: 説明`、終了コードは成功時 0、違反・解析失敗時 1 です。`mise run check:xaml` を実行入口とし、`test:xaml` で検査用 XAML に対する診断を確認します。`verify` は `check:xaml` をビルド前に実行します。回帰検証の実行順は [テストの分担](#test-boundary) に従い、`package` もこの検査を通します。将来ネイティブ実装へ変更する場合も、この入口と診断・終了コードを維持します。

<a id="code-quality"></a>
### 静的解析と整形

C# と XAML の書式・コード規約は、このディレクトリの次のファイルを正本とします。通常のコード規約違反は Debug ビルドでは警告、Release・Mock ビルドではエラーです。層の依存検査の `ARCH001`・`ARCH002` は Debug・Release・Mock のすべてでエラーとします。

| ファイル | 役割 |
| --- | --- |
| `.editorconfig` | 書式と命名規則などのコード規約。ビルド時に検査する（`EnforceCodeStyleInBuild`） |
| `Directory.Build.props` | 全プロジェクトに .NET アナライザーの推奨セット（`latest-recommended`）と SonarAnalyzer を適用し、Debug 以外では警告をエラーにする |
| `app/App.csproj` | WPF 固有の WpfAnalyzers と、Analyzer として参照する ArchitectureAnalyzer を適用する |
| `analyzers/ArchitectureAnalyzer/LayerDependencyAnalyzer.cs` | 製品内の層の依存と旧 Model 名前空間の型宣言を検査する |
| `Settings.XamlStyler` | XAML の整形規則（字下げ 2、属性が 3 つ以上なら 1 行に 1 属性、属性の並べ替え） |
| `.config/dotnet-tools.json` | XAML の整形に使う XamlStyler（`xstyler`）と網羅率を計測する dotnet-coverage の版 |

<a id="layer-analysis"></a>
#### 層の依存検査

`ArchitectureAnalyzer` は `netstandard2.0`・Roslyn 4.14.0 のアナライザーで、製品のビルド時に適用します。製品アセンブリ名を名前空間のルートとし、同一製品内の `View`・`ViewModel`・`Domain`・`Infrastructure` 以下の型・メンバーへの実際の参照を意味解析で検査します。同層と View → ViewModel、ViewModel → Domain、Infrastructure → Domain だけを許可し、それ以外は `ARCH001` です。View から Domain への直接参照も違反です。

起動・設定・`Compatibility` など 4 領域外の型と外部ライブラリは、この依存検査の対象外です。別途 `ARCH002` で `<製品ルート>.Model` 以下の型宣言を禁止します。alias・`global::`・ジェネリック・継承・属性・const・enum 等の参照と生成 C# も解析します。未使用の `using` 宣言だけでは依存と判定しません。

Domain の純粋性と record メンバーの不変性は [状態の分担と Domain](#state-domain) に従い、コードレビューで確認します。アナライザーによる自動強制の対象ではありません。検査の回帰テストは `tests/unit/Architecture/ArchitectureTests.cs` に置き、`test:unit` と `verify` に含めます。アナライザー DLL と Roslyn の実行資材は製品配布に含めません。

#### コード規約と整形

主なコード規約は次のとおりです。
- **フィールド名**: private フィールドは `_` 始まりの camelCase とします。CommunityToolkit.Mvvm の `[ObservableProperty]` のフィールドも同じで、`_noteTitle` から `NoteTitle` プロパティが生成されます。
- **`this.`**: 付けません。コンストラクターでは `_notes = notes;` と書きます。
- **その他の命名**: インターフェースは `I` 始まり、型・メンバーは PascalCase、引数・ローカル変数は camelCase、定数は PascalCase です。
- **構成**: 名前空間はファイルスコープ、`using` は名前空間の外に置きます。使われない private メンバーと readonly にできるフィールドを検出します。
- **SonarAnalyzer の規則**: 既定プロファイル（Sonar way）の 321 規則に加え、既定では無効の規則のうち 104 個を `.editorconfig` で有効にしています（規則ごとに題名を添えて記載）。閾値はすべて Sonar の既定値です。
  - 複雑度と規模（14 個）: 認知的複雑度 15（S3776）、サイクロマティック複雑度 10（S1541）、メソッド 80 行（S138）、引数 7 個（S107）、継承の深さ 5（S110）、クラス結合度（S1200）、制御構文の入れ子 3（S134）、式の複雑さ（S1067）、ファイルの行数（S104）、`switch` の規模（S1151・S1479・S1821）、行の長さ（S103）、型引数の数（S2436）
  - 不具合につながる書き方（19 個）: 非同期メソッドを同期的に待つ（S4462）、IDisposable のフィールドの破棄漏れ（S2931・S2952）など
  - 書き方を厳しくする規則（71 個）: 波かっこ必須（S121）、マジックナンバー（S109）、文字列比較とカルチャの明示（S4058・S4056）、すべての例外の catch（S2221）など
- **有効にしない Sonar の規則**: 次の 40 個は有効にしません。
  - 既存の仕組みと重複する 6 個: 命名、不要な `using`、書式
  - このアプリに不適切・無関係な 33 個: WPF の UI スレッドで誤動作する `ConfigureAwait(false)` の強制、Azure Functions・Blazor・ロガー・P/Invoke・COM 用の規則、`DateTimeOffset` やテスト用の時刻提供の強制など設計の変更を伴う規則
  - S3242（引数はより汎用的な型に）: .NET アナライザーの CA1859（private メンバーは性能のため具体的な型に）と逆の指摘になるため、無効にします。
- **例外**: 理由を明記して、次のものを対象外にします。
  - テストの CA1861: 期待値の配列を呼び出し箇所に書くため。
  - シナリオ E2E の S3776・S2760: 文書の手順と条件違いを 1 つのテストに並べ、手順ごとに成功時と失敗時を分ける構成のため。
  - ウィンドウ枠のイベントハンドラーの S2325: XAML から結び付けるにはインスタンスメソッドが必要なため。
  - 画面操作と起動処理の S2221: どんな失敗でも原因を表示する受け口のため。メモリ不足（`OutOfMemoryException`）は捕まえません。

`mise run format` は C# を `dotnet format`、XAML を XamlStyler で整形します。`mise run check:format` は変更せずに整形漏れとコード規約の違反を検査し、`verify` はビルドの前にこれを実行します。
