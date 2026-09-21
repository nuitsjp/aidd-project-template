# AIDD React Template

Reactの対話制御からNode.jsの機能、SQLiteへの確定までを通す参照実装です。

## 新規プロジェクトの生成

新規プロジェクトは [配布元の導入手順](https://github.com/nuitsjp/aidd-project-template#2-新規プロジェクトへの導入) に従って生成します。`react-template/` は同一チェックアウトの `template/` に重ねる React 固有の差分であり、単体では実行しません。

## 実行・確認

生成後の環境構築、起動、切り替え、テスト、配備の手順は [プロジェクト定義の実行手順](docs/project.md#commands) に集約しています。生成したプロジェクトのルートで実行します。

## 参照先

| 文書 | 内容 |
| --- | --- |
| [architecture.md](docs/architecture.md) | 全体構成、実現パターンの適用条件、設計上の制約 |
| [UCP-1](docs/design/UCP-1.md)・[UCP-2](docs/design/UCP-2.md) | 実現パターンごとの具体設計と結果確定点 |
| [data.md](docs/design/data.md) | 保存方式、テーブル、データ制約 |
| [architecture-react.md](docs/architecture-react.md) | React・Node.js の責務、依存方向、並列 E2E |
| [project.md](docs/project.md) | 目的、制約、外部事実、認証・配備・DB運用、実行手順 |
| [document-policy.md](docs/document-policy.md) | 適用する標準、固有差分、正本の責務 |

サンプルのUIやタイトル一意制約、文字数制限は参照用仕様です。製品開発時は `backend/features/notes/`・`frontend/src/usecases/` とE2Eを製品のユースケースに置き換えます。テンプレート名が残る `package.json`、`frontend/index.html`、`frontend/src/app/Shell.tsx`、`backend/http/auth.ts`、`.github/workflows/react-template.yml` も製品名へ置き換えます。

サンプルの仕様は製品の承認済み仕様として扱いません。導入時は [導入開始手順](https://github.com/nuitsjp/aidd-project-template#3-初期セットアップと最初のユースケース) を確認し、製品固有の仕様を定義します。

共通資材の更新対象と固有資材の管理範囲は [文書方針](docs/document-policy.md#adoption) に従います。

`.github/workflows/react-template.yml` は生成プロジェクト用のCI例です（生成後のルートで実行することを前提としています）。
