# Wails Template

Windows用のユースケース駆動参照アプリです。対話制御を React、機能と保存を Go に配置し、メモ編集・CSV取り込み・アプリ内更新を実装しています。

**検証状況:** Go の独立パッケージテストは実行済みですが、提供環境の外部接続制限により依存取得、Wails全体ビルド、React型検査・E2E、Windows実機での起動・NSIS更新は未検証です（詳細は [検証結果](docs/project.md#verification) 参照）。コンパイル済み配布物ではなく、ビルド対象のソース一式です。

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

生成されたプロジェクトのルートでコマンドを実行します。事前に Go 1.25以上、Node.js 22.16以上、Python 3.9以上、WebView2 Evergreen Runtime を導入し、`go`・`node`・`npm`・`python` が PATH 上で使えるようにしてください。NSIS 3.11以上はインストーラー作成時のみ必要です。Go の自動ツールチェーン取得を禁止する環境では、依存モジュールが要求する Go 版も事前に導入してください。

```powershell
cd ../my-wails-app
node scripts/run.mjs setup
node scripts/run.mjs dev
```

`setup` は指定版の Wails CLI をローカルの `.tools/` に導入し、Go/npm 依存、実際の Go バインディング、ルートツリーを生成します。初回は外部ネットワークが必要です。依存取得できない環境で架空の lockfile を作らないため、`go.sum` と `frontend/package-lock.json` は初回生成とし、初回成功後に両方をコミットしてください。以後は `npm ci` を使用します。直接依存の版は固定済みですが、初回解決前の推移的依存は未固定です。

| コマンド（先頭に `node scripts/run.mjs`） | 内容 |
| --- | --- |
| `dev` | Windowsアプリを起動し、Go・React の変更を監視 |
| `dev:mock` | 試験用固定データで起動（実データは変更しない） |
| `build` | 本番実行ファイルを `bin/` に生成 |
| `package` | ビルド後、ユーザー単位の未署名 NSIS インストーラーを `bin/` に生成 |
| `server` | Go 実処理を使うブラウザ確認用サーバーを localhost:34115 で起動 |
| `verify` | 生成・型検査・Lint・テスト・文書検査・server E2E を一括実行 |
| `test:core` | Go の機能・保存・更新検証を実行 |

初回の E2E 実行前に `cd frontend; npx playwright install chromium; cd ..` を実行してください。`server` は開発・検証用であり、LAN へ公開しません。終了は Ctrl+C です。

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

サンプルの UI やデータ設計は参照用です。製品開発時は `usecases/`・`features/notes`・`internal/notes` と対応するルート・テストを製品固有の実装へ置き換え、製品固有の UC・データ設計・合意記録を改めて定義してください。参照実装の記録を製品の合意済み仕様に転用しません。

`.github/workflows/windows.yml` は生成プロジェクト用の CI 例です。生成後のルートで `setup`・`verify`・`build`・`package` を実行し、`SOURCE_DIR` は `.` のまま使用します。`wails-template/` をチェックアウト上で単独実行する CI ではありません。ライセンスは [MIT](LICENSE) です。
