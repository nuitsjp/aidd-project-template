# 文書方針

適用する標準、モック駆動開発の範囲、正本の配置、および参照実装の扱いを定義します。

<a id="adoption"></a>
## 1. 適用する標準

導入状態: **未適用**。採用先で以下の版と固有差分を確定した後に「適用済み」へ更新してください。

| 項目 | 内容 |
| --- | --- |
| 配布元・版 | [aidd-project-template](https://github.com/nuitsjp/aidd-project-template) / 版19 |
| 設計・文書標準 | [版15](standards/design-and-documentation.md) |
| モック標準 | [版17](standards/mock-driven-development.md) |
| React拡張 | 0.1.2 / React・Node.js・SQLite・並列 E2E の参照実装 |
| 固有差分 | React と Node.js の責務分担、SQLite の保存、実処理までの並列 E2E を提供します。共通構造の補足は architecture-react.md に置きます |

生成時は `template/` を先にコピーし、`react-template/` の内容で上書きします。共通資材（`AGENTS.md`、`docs/standards/`、検査スクリプト、`LICENSE`）は共通側から取得し、`docs/project.md` と本書は React 差分側で個別管理します（共通版との部分マージは行いません）。

<a id="mock-scope"></a>
## 2. モック駆動開発の適用範囲

採用先で新設・変更する系列へ適用します。参照実装には仕様確認用の固定データを残さず、既定の実行経路は実処理とします。テスト用データと実処理の検証は区別します。

<a id="sources"></a>
## 3. 文書の役割

| 正本 | 内容 |
| --- | --- |
| 本書 | 適用する標準、固有差分、文書の責務 |
| [project.md](project.md) | 目的、制約、ユースケース一覧、外部事実、実行・検証手順 |
| [architecture.md](architecture.md) | 現在のシステム構成、実現パターン、設計上の制約、テーブル設計 |
| [architecture-react.md](architecture-react.md) | React・Node.js の責務と依存方向 |
| usecases/UC-n.md | 現在のシナリオと受け入れ条件 |
| standards/ | 適用する標準の原文 |
| [README.md](../README.md) | 起動・参照案内 |
| [AGENTS.md](../AGENTS.md) | 作業時の参照先と注意点 |

既存の正本で扱える事項について管理文書を新設しません。ログ・画像・試験 DB はリポジトリで追跡しません。

<a id="agreements"></a>
## 4. 仕様変更と参照実装の扱い

適用する標準・固有差分・正本の責務と、製品として定義した現在の要件・制約・仕様・完了条件・データ設計を変更する場合は、[変更手続き](standards/design-and-documentation.md#agreement-changes) に従います。

参照実装の仕様は製品の承認済み仕様として扱いません。導入時に製品固有の目的、制約、シナリオ、受け入れ条件を各正本へ定義します。
