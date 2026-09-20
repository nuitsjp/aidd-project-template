# Wails Template

Windows用のユースケース駆動参照アプリ。対話をReact、機能と保存をGoに配置し、メモ編集・CSV取り込み・アプリ内更新を実装しています。

**検証状況:** Goの独立パッケージのテストは実行済みですが、提供環境の外部通信制限により依存取得、Wails全体ビルド、Reactの型検査・E2E、Windowsでの起動・NSIS更新は未検証です。詳細は[検証結果](docs/project.md#verification)に集約しています。コンパイル済み配布物ではなく、ビルド対象のソース一式です。

## 配置

`wails-template/` は、同じチェックアウトにある `template/` を土台へ重ねるWails差分です。単独で実行せず、リポジトリのルートから次を実行して新規プロジェクトを生成します。生成時は `template/` を先にコピーし、`wails-template/` の内容で上書きし、ルートの `LICENSE` を配置します。

```text
aidd-project-template/
├── template/
└── wails-template/       ← 生成時に重ねる差分（単独実行しない）
```

```powershell
mise run init:wails ../my-wails-app
```

## Windowsで開始

生成されたプロジェクトのルートでコマンドを実行します。事前にGo 1.25以上、Node.js 22.16以上、Python 3.9以上、WebView2 Evergreen Runtimeを導入し、`go`・`node`・`npm`・`python`がPATH上で使えるようにします。NSIS 3.11以上はインストーラー作成時だけ必要です。Goの自動ツールチェーン取得を禁止する環境では、依存モジュールが要求するGo版も事前に導入してください。

PowerShellで次を実行します。

```powershell
cd ../my-wails-app
node scripts/run.mjs setup
node scripts/run.mjs dev
```

`setup`は指定版のWails CLIをローカルの`.tools/`に導入し、Go/npm依存、実際のGoバインディング、ルートツリーを生成します。初回は外部ネットワークが必要です。**依存取得できない提供環境で架空のlockfileを作らないため、`go.sum`と`frontend/package-lock.json`は初回生成になります。初回成功後は両方をコミットしてください。以後はnpm ciを使用します。** 直接依存の版は固定済みですが、初回解決前の推移的依存は未固定です。

| コマンド（先頭は `node scripts/run.mjs`） | 内容 |
| --- | --- |
| `dev` | Windowsアプリを起動。Go・Reactの変更を監視 |
| `dev:mock` | 同じアプリを試験用固定データで起動。実データは変更しない |
| `build` | 本番Windows実行ファイルを `bin/` に生成 |
| `package` | ビルド後、ユーザー単位の未署名NSISを `bin/` に生成 |
| `server` | Go実処理を使うブラウザ確認用サーバーをlocalhost:34115で起動 |
| `verify` | 生成・型検査・Lint・テスト・文書検査・server E2E |
| `test:core` | Goの機能・保存・更新検証を実行 |

初めてE2Eを行う前に、`cd frontend; npx playwright install chromium; cd ..` を実行します。`server`は開発・検証用であり、LANへ公開しません。終了はCtrl+Cです。

## 確認できる実装

メモの作成・選択・編集・保存、入力エラーと未保存確認、CSV入力と確認画面をまたぐ下書き、Goによる一括保存・進捗通知・中止要求を含みます。CSVの列は `title,body` です。入力例は次のとおりです。

```csv
title,body
設計メモ,対話はフロントエンドで制御する
実装メモ,保存はGoで確定する
```

進捗は実処理から通知し、見せるための待ち時間は入れていません。

初回起動時のメモは空です。保存先はOSのユーザー設定領域内のアプリID配下です。別の試験データを使う場合だけ、環境変数 `WAILS_DATA_DIR` に絶対パスを指定します。アプリの更新・アンインストールではこのデータを消しません。

## 配布と更新の設定

アプリ名・識別子・版・実行ファイル名・更新元・更新用公開鍵は [`build/app.json`](build/app.json) で設定します。初期値の更新元と鍵は空です。存在しない公開リリースや共通秘密鍵は同梱しません。更新コードと画面は実装済みで、配布者が一度設定すると利用できます。

[更新元と署名の設定手順](docs/project.md#release)に従って、初回インストーラーを作る**前に**公開鍵と更新元を埋め込みます。共有フォルダと公開GitHub Releasesに対応します。privateリポジトリの認証・鍵ローテーション・差分更新は含めません。

NSISはアプリ本体、スタートメニュー、アンインストール情報を管理します。アプリ内更新では、取得物の検証と利用者の確認後にNSISへ引き渡し、旧PIDの終了を最大60秒待って適用・再起動します。適用失敗時の自動ロールバックはありません。

この版のNSISはWebView2の既存導入を検査し、未導入なら導入を案内して停止します。Runtimeのオンライン自動取得は組み込んでいません。[Microsoft公式のEvergreen Installer](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution)を先に導入してください。未署名による実行制限は、更新用Ed25519署名では解除されません。

## 設計・規則・採用

[アーキテクチャ](docs/architecture.md) / [Wails補足](docs/architecture-wails.md) / [プロジェクト定義と検証](docs/project.md) / [採用記録](docs/document-policy.md)

サンプルを製品へ持ち込む際は、`usecases/`・`features/notes`・`internal/notes`と対応するルート・テストを置き換えます。製品固有のUC・データ設計・合意記録は改めて定義し、参照実装の記録を製品の合意済み仕様に転用しません。

`.github/workflows/windows.yml`は、`mise run init:wails`で生成したプロジェクトのルートに置いて実行するCI例です。`setup`・`verify`・`build`・`package`を生成後のルートで実行し、`SOURCE_DIR`は`.`のまま使います。`wails-template/`をチェックアウト上で単独実行するCIではありません。

ライセンスは [MIT](LICENSE) です。
