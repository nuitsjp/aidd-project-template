# Wails Template

Windows用のユースケース駆動参照アプリです。対話制御を React、機能と保存を Go に配置し、メモ編集・CSV取り込み・アプリ内更新を実装しています。

**検証状況:** 生成先で依存取得、Wailsバインディング生成、型検査・Lint・Go/Vitestテスト、server E2E、Windows向け本番ビルド、NSISインストーラー作成に合格しました。Windows実機では、インストール、WebView2での起動、多重起動、終了確認、共有フォルダ経由のアプリ内更新（0.1.0→0.2.0）、アンインストール後のデータ保持を確認しました。画面へのキー入力を伴う操作の実機確認と未署名実行制御は未確認です（詳細は [検証結果](docs/project.md#verification) 参照）。配布物はビルド対象のソース一式です。

## 配置

`wails-template/` は、同一チェックアウトの `template/` に重ねるWails固有の差分です。単独実行せず、リポジトリのルートから以下を実行して新規プロジェクトを生成します（`template/` を先にコピーし、`wails-template/` の内容で上書きしてルートの `LICENSE` を配置します）。

```text
aidd-project-template/
├── template/
└── wails-template/       ← 生成時に重ねる差分（単独実行しない）
```

```powershell
mise run init:wails ../my-wails-app
```

## Windowsで開始

生成されたプロジェクトのルートでコマンドを実行します。事前に Go 1.25以上、Node.js 22.16以上、Python 3、WebView2 Evergreen Runtime を導入し、`go`・`node`・`npm`・`python` が PATH 上で使えるようにしてください。mise を使う場合は同梱の `mise.toml` で検証済みの版を導入できます（`mise install`）。NSIS 3.11以上はインストーラー作成時のみ必要です。Go の自動ツールチェーン取得を禁止する環境では、依存モジュールが要求する Go 版も事前に導入してください。

```powershell
cd ../my-wails-app
node scripts/run.mjs setup
node scripts/run.mjs dev
```

`setup` は指定版の Wails CLI をローカルの `.tools/` に導入し、Go/npm 依存、実際の Go バインディング、ルートツリーを生成します。初回は外部ネットワークが必要です。`go.sum` と `frontend/package-lock.json` は検証時に生成したものを同梱しており、npm 依存は `npm ci` で lockfile どおりに導入されます。依存を変更したときは両ファイルを更新してコミットしてください。

Wails本体・JavaScript Runtimeは検証する組合せで更新し、`setup` でGo依存の版に対応するCLIを入れ直して生成コードを再生成します。`verify` とWindowsビルドを実行し、実機確認の範囲も記録します。採用後の依存定義・ロックは採用先が管理します。最低対応版は上記の前提、検証に使ったツール版は `mise.toml` と [検証結果](docs/project.md#verification) を参照します。

| コマンド（先頭に `node scripts/run.mjs`） | 内容 |
| --- | --- |
| `dev` | Windowsアプリを起動し、Go・React の変更を監視 |
| `dev:mock` | 試験用固定データで起動（実データは変更しない） |
| `build` | 本番実行ファイルを `bin/` に生成 |
| `package` | ビルド後、ユーザー単位の未署名 NSIS インストーラーを `bin/` に生成 |
| `server` | Go 実処理を使うブラウザ確認用サーバーを localhost:34115 で起動 |
| `verify` | 生成・型検査・Lint・テスト・文書検査・server E2E を一括実行 |
| `test:core` | Go の機能・保存・更新検証を実行 |

初回の E2E 実行前に `cd frontend; npx playwright install --only-shell chromium; cd ..` を実行し、導入済み Playwright に対応する headless shell を取得してください。E2E はヘッドレスで実行します。`server` は開発・検証用であり、LAN へ公開しません。終了は Ctrl+C です。

通常の Chrome / Chromium を明示して検証する場合は、`PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` に実行ファイルの絶対パスを設定します。その環境で既定引数 `--disable-extensions` による起動失敗を確認した場合だけ、`PLAYWRIGHT_IGNORE_DISABLE_EXTENSIONS=1` も設定して当該引数を除外します。指定例は次のとおりです。自動的なブラウザー切り替えは行いません。

```powershell
$env:PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH = Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'
$env:PLAYWRIGHT_IGNORE_DISABLE_EXTENSIONS = '1'
node scripts/run.mjs verify
```

## 確認できる実装

メモの作成・選択・編集・保存、入力エラーと未保存確認、CSV 入力と確認画面をまたぐ下書き、Go による一括保存・進捗通知・中止要求を含みます。CSV は `title,body` 列形式です。

```csv
title,body
設計メモ,対話はフロントエンドで制御する
実装メモ,保存はGoで確定する
```

進捗は実処理から通知し、見せるための待ち時間は入れていません。

初回起動時のメモは空です。データは OS のユーザー設定領域（アプリ ID 配下）に保存されます。別の試験データを使う場合だけ、環境変数 `WAILS_DATA_DIR` に絶対パスを指定します。更新やアンインストールでデータは削除されません。

## 配布と更新の設定

アプリ名・識別子・版・実行ファイル名・更新元・更新用公開鍵は [`build/app.json`](build/app.json) で設定します。初期状態の更新元と鍵は空で、存在しない公開リリースや共通秘密鍵は同梱しません。更新コードと画面は実装済みで、配布者が設定すると利用できます。

[更新元と署名の設定手順](docs/project.md#release) に従い、初回インストーラー作成前に公開鍵と更新元を設定してください。共有フォルダと公開 GitHub Releases に対応し、private リポジトリの認証・鍵ローテーション・差分更新は含めません。

NSIS はアプリ本体、スタートメニュー、アンインストール情報を管理します。アプリ内更新では、取得物の検証と利用者の確認後に NSIS へ引き渡し、旧 PID の終了を最大60秒待って適用・再起動します。適用失敗時の自動ロールバックはありません。

この版の NSIS は WebView2 の既存導入を検査し、未導入なら案内して停止します。Runtime のオンライン自動取得は組み込んでいません。[Microsoft公式のEvergreen Installer](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/distribution) を先に導入してください。未署名による実行制限は、更新用 Ed25519 署名では解除されません。

## 設計・規則・採用

- [アーキテクチャ](docs/architecture.md)
- [Wails補足](docs/architecture-wails.md)
- [プロジェクト定義と検証](docs/project.md)
- [採用記録](docs/document-policy.md)

サンプルの UI やデータ設計は参照用です。製品開発時は下表の「サンプル」を製品固有の実装へ置き換え、製品固有の UC・データ設計・合意記録を改めて定義してください。参照実装の記録を製品の合意済み仕様に転用しません。

| 区分 | 対象 |
| --- | --- |
| 残す基盤 | `main.go`（Service 登録の骨格）、`internal/desktop`・`appstate`・`fault`・`diagnostics`・`updates`、`cmd/release`、`frontend/src/app`・`shared`・`features/application`・`features/updates`、`usecases/update-app`、`routes/__root.tsx`・`routes/updates.tsx`、`build/`、`scripts/`、`Taskfile.yml` |
| 置き換えるサンプル | `internal/notes`、`frontend/src/features/notes`、`usecases/edit-notes`・`import-notes`、`routes/notes.tsx`・`routes/import*.tsx`・`routes/index.tsx` の遷移先、`Shell.tsx` のナビゲーションと `subscribeNotes` の購読、`tests/fixtures/notes.ts`、`tests/e2e/usecases.spec.ts`、`vite.config.ts`・`tsconfig.json`・`eslint.config.mjs` の `@notes-service` 設定、`docs/usecases/UC-1〜3.md`、`docs/architecture.md` の UCP-1・UCP-2 と第5節 |

新しい機能領域のモック合成点の作り方は [Wails補足第4節](docs/architecture-wails.md#4-モックと検証境界) を参照します。

採用時は [導入開始手順](https://github.com/nuitsjp/aidd-project-template#3-初期セットアップと最初のユースケース) を確認し、サンプルの仕様・合意を引き継がずに製品固有の内容を定義します。

上表の「残す基盤」も生成後は採用先が管理するコードです。継続同期する対象ではありません。配布元から一組で更新する対象は `AGENTS.md`・標準2件・文書検査器に限り、製品文書・実装・設定・DB移行履歴は上書きしません。採用元コミットと固有差分を [文書方針](docs/document-policy.md#adoption) に記録し、[配布元の更新手順](https://github.com/nuitsjp/aidd-project-template#5-配布版の更新取り込み) に従います。

`.github/workflows/windows.yml` は生成プロジェクト用の CI 例です。main への push と pull request で `setup`・`verify`・`build`・`package` を実行し、インストーラーを artifact として保存します。`wails-template/` をチェックアウト上で単独実行する CI ではありません。ライセンスは [MIT](LICENSE) です。
