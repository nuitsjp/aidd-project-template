# AIDD React Template

Reactの対話制御からNode.jsの機能、SQLiteへの確定までを通す参照実装です。

## 配置

`react-template/` は、同一チェックアウトの `template/` に重ねるReact固有の差分です。単独実行せず、リポジトリのルートから以下を実行して新規プロジェクトを生成します（`template/` を先にコピーし、`react-template/` の内容で上書きしてルートの `LICENSE` を配置します）。

```text
aidd-project-template/
├── template/
└── react-template/       ← 生成時に重ねる差分（単独実行しない）
```

## 実行・確認

生成、環境構築、起動、切り替え、テスト、配備の手順は [プロジェクト定義の実行手順](docs/project.md#commands) に集約しています。生成先のルートで手順を実行し、`react-template/` 単体では実行しません。

## 参照先

| 文書 | 内容 |
| --- | --- |
| [architecture.md](docs/architecture.md) | 実装パターン、保存の確定点、テーブル、テスト分離 |
| [architecture-react.md](docs/architecture-react.md) | 共通の責務と依存方向 |
| [project.md](docs/project.md) | 目的、制約、外部事実、認証・配備・DB運用、実行手順 |
| [document-policy.md](docs/document-policy.md) | 適用する標準、固有差分、正本の責務 |

サンプルのUIやタイトル一意制約、文字数制限は参照用仕様です。製品開発時は `backend/features/notes/`・`frontend/src/usecases/` とE2Eを製品のユースケースに置き換えます。

サンプルの仕様は製品の承認済み仕様として扱いません。導入時は [導入開始手順](https://github.com/nuitsjp/aidd-project-template#3-初期セットアップと最初のユースケース) を確認し、製品固有の仕様を定義します。

`.github/workflows/react-template.yml` は生成プロジェクト用のCI例です（生成後のルートで実行することを前提としています）。
