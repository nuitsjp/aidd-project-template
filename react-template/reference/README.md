# メモアプリの参照実装

Reactの対話制御からNode.jsの機能、SQLiteへの確定までを通すサンプルです。生成時にこのアプリ一式を製品ルートへコピーします。製品開発を開始しても、このディレクトリを参照用として残します。

## 実行・確認

生成後の環境構築、起動、切り替え、テスト、配備の手順は [プロジェクト定義の実行手順](docs/project.md#commands) に集約しています。生成先の `reference/` で実行します。

## 参照先

| 文書 | 内容 |
| --- | --- |
| [architecture.md](docs/architecture.md) | 全体構成、実現パターンの適用条件、設計上の制約 |
| [UCP-1](docs/design/UCP-1.md)・[UCP-2](docs/design/UCP-2.md) | 実現パターンごとの具体設計と結果確定点 |
| [data.md](docs/design/data.md) | 保存方式、テーブル、データ制約 |
| [architecture-react.md](docs/architecture-react.md) | React・Node.js の責務、依存方向、並列 E2E |
| [project.md](docs/project.md) | 目的、制約、外部事実、認証・配備・DB運用、実行手順 |

サンプルのUIやタイトル一意制約、文字数制限は参照用仕様です。製品開発時は製品ルートの `backend/features/notes/`・`frontend/src/usecases/` とE2Eを製品のユースケースに置き換えます。テンプレート名が残る `package.json`、`frontend/index.html`、`frontend/src/app/Shell.tsx`、`backend/http/auth.ts`、`.github/workflows/react-template.yml` も製品名へ置き換えます。

サンプルの仕様は製品の承認済み仕様として扱いません。製品の仕様・設計は親ディレクトリの `docs/` で管理します。導入時は [導入開始手順](https://github.com/nuitsjp/aidd-project-template#3-初期セットアップと最初のユースケース) を確認し、製品固有の仕様を定義します。

共通資材の更新対象と固有資材の管理範囲は [ルートの文書方針](../docs/document-policy.md#adoption) に従います。
