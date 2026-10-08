# WPF アーキテクチャ

本書の実装パスは `reference/` を起点とします。製品の設計は生成先ルートの `docs/` で管理します。

## 1. 起動と画面

`app/App.csproj` は `net481`・C# `latest` の単一 WPF プロジェクトです。起動処理は Kamishibai の Generic Host で Model と View / ViewModel を登録します。アプリ ID・データ領域・実処理またはモックのモードに対応するインスタンス判定を行い、同じ対象の 2 回目の起動は新しい通常ウィンドウを開きません。

通常の画面は一覧、編集、一括入力・確認を `NavigationFrame` 内で切り替えます。Kamishibai の `IPresentationService` が遷移と確認ダイアログを担当し、`AddPresentation` で View と ViewModel を対応付けます。CommunityToolkit.Mvvm の通知と Command を使います。画面の XAML に SQL や業務確定を置きません。

## 2. 対話と Model

| 領域 | 保持するもの・責務 |
| --- | --- |
| `View/` | XAML、表示、入力の Binding |
| `ViewModel/` | 編集下書き、一括入力、確認内容、実行中状態、入力エラー・保存結果、未保存確認 |
| `Model/UseCase/` | 入力のプレビュー、確認内容の保持と確定、必要な機能の組合せ |
| `Model/Domain/Notes/` | メモの型・入力規則と一覧・保存・削除・一括登録の契約 |
| `Model/Infrastructure/Sqlite/` | SQLite の接続・初期化・SQL 移行 |
| `Model/Infrastructure/Sqlite/Notes/` | メモの SQLite 実装 `NotesService` |

Infrastructure は技術別に配置し、その下を業務領域別に分けます。`Database.cs` と `Migrations/` は Sqlite 直下に置きます。モックの `InMemoryNotesService` は `Domain/Notes/` に置き、起動時に同じ契約へ差し替えます。

保存処理中は保存・削除・離脱を重ねません。下書きは保存・破棄の完了まで ViewModel に保持し、失敗や一覧再取得で置き換えません。未保存の編集または一括入力がある状態の画面離脱・終了では確認を求め、取り消し時は現在の画面に留まります。

入力境界で値を検証し、版競合・対象なし・保存失敗を対話で表示します。保存時は返された確定済みの値で編集画面を更新します。利用者が一覧へ戻る操作で生成される一覧 ViewModel が再取得し、その失敗は `ListError` に表示します。完了済みの保存を失敗に戻して再実行を促しません。

## 3. 一括入力と確認

1 行を 1 メモとし、最初のタブの前をタイトル、後ろを本文にします。タブがない行は空本文、追加のタブは本文に残します。空白だけの行は無視し、CRLF / LF を受け付けます。値と件数の制約は [データ設計](design/data.md) を参照します。

プレビューは DB を更新せず正規化後の一覧を表示します。入力変更で確認内容を破棄し、再確認するまで一括登録を実行しません。確定時は確認の元となった入力を再検証し、短いトランザクションで全件を確定します。

<a id="mock-boundary"></a>
## 4. モック境界

起動時の DI の組み立てで、ViewModel が利用する Model のサービスを実 SQLite または固定データの実装へ切り替えます。View と ViewModel、入出力型は両方で共有します。`dev:mock` は Mock ビルドと明示的な `--mock` を使い、画面にモックであることを表示します。

固定データの実装は Mock 構成にだけ含めます。Debug / Release のアセンブリと配布 ZIP に固定データを含めず、実処理の失敗をモックで補いません。

<a id="test-boundary"></a>
## 5. テストの分担

単体テストは入力規則、Model の共有処理、ViewModel の下書き・確認失効・実行状態を対象にします。統合テストは実 SQLite を通し、版競合と一括登録のロールバックを確認します。各テストに専用の一時 DB を用意します。

UI テストは Friendly の `WindowsAppFriend` で実アプリに接続します。`Drivers/` にメインウィンドウ、一覧、編集、一括入力、確認ダイアログの専用ドライバーを置き、シナリオはドライバーの型付き操作と表示値を使って検証します。WPF の部品は XAML の `x:Name` で宣言したフィールドを `dynamic` から参照し、画面は `VisualTree().ByType<画面型>()` で取得します。確認ダイアログのドライバーは `NativeMessageBox` を使います。

`Support/AppSession` はアプリの起動・終了と `--data-dir` による一時 DB の分離を担当し、`NoteDatabase` は別接続による保存結果の検証を担当します。UI テストは同時に実行せず、終了後に一時データを片付けます。

シナリオの UI テストは `tests/e2e/<ユースケース名>/<シナリオ名>.cs` に1ファイルずつ配置し、開始条件または分岐条件、手順、受け入れ条件を文書と同じ番号で区切ります。主成功、削除と離脱の確認、入力・保存失敗、更新競合、保存確定後の一覧取得失敗、一括確認の失効とロールバックを実 UI と SQLite で検証します。ViewModel の状態遷移と DB の境界条件は単体・統合テストで補強します。モック構成の起動・再起動確認は `tests/e2e/Runtime/` に分離します。実行コマンドと実機確認の条件は [プロジェクト定義](project.md#commands) に記載します。
