# Wails Template

Windows用のユースケース駆動参照アプリです。対話制御を React、機能と保存を Go に配置し、メモ編集・CSV取り込み・アプリ内更新を実装しています。

配布物はビルド対象のソース一式です。

## 配置

`wails-template/` は、同一チェックアウトの `template/` に重ねるWails固有の差分です。単独実行せず、リポジトリのルートから以下を実行して新規プロジェクトを生成します（`template/` を先にコピーし、`wails-template/` の内容で上書きしてルートの `LICENSE` を配置します）。

```text
aidd-project-template/
├── template/
└── wails-template/       ← 生成時に重ねる差分（単独実行しない）
```

## Windowsで開始

必要な環境、生成、セットアップ、起動、テスト、配布、更新の手順は [プロジェクト定義の実行手順](docs/project.md#commands) に集約しています。生成先のルートで手順を実行し、`wails-template/` 単体では実行しません。

## 確認できる実装

メモの作成・選択・編集・保存、入力エラーと未保存確認、CSV 入力と確認画面をまたぐ下書き、Go による一括保存・進捗通知・中止要求を含みます。CSV は `title,body` 列形式です。

```csv
title,body
設計メモ,対話はフロントエンドで制御する
実装メモ,保存はGoで確定する
```

進捗は実処理から通知し、見せるための待ち時間は入れていません。

初回起動時のメモは空です。データは OS のユーザー設定領域（アプリ ID 配下）に保存されます。別の試験データを使う場合だけ、環境変数 `WAILS_DATA_DIR` に絶対パスを指定します。更新やアンインストールでデータは削除されません。

## 配布と更新

アプリ設定、署名、インストーラー、更新元、WebView2 の条件は [更新元と署名の設定手順](docs/project.md#release) に集約しています。

## 設計・規則

- [アーキテクチャ](docs/architecture.md)
- [Wails補足](docs/architecture-wails.md)
- [プロジェクト定義](docs/project.md)
- [文書方針](docs/document-policy.md)

サンプルの UI やデータ設計は参照用です。製品開発時は `usecases/`・`features/notes`・`internal/notes` と対応するルート・テストを製品固有の実装へ置き換え、製品固有の UC・データ設計を定義してください。参照サンプルの仕様は製品の承認済み仕様として扱いません。

導入時は [導入開始手順](https://github.com/nuitsjp/aidd-project-template#3-初期セットアップと最初のユースケース) を確認し、製品固有の内容を定義します。

`.github/workflows/windows.yml` は生成プロジェクト用の CI 例です。生成後のルートで `setup`・`verify`・`build`・`package` を実行し、`SOURCE_DIR` は `.` のまま使用します。`wails-template/` をチェックアウト上で単独実行する CI ではありません。ライセンスは [MIT](LICENSE) です。
