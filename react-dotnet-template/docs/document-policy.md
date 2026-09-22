# 文書方針

適用する標準、モック駆動開発の範囲、正本の配置、および参照実装の扱いを定義します。

<a id="adoption"></a>
## 1. 適用する標準

導入状態: **未適用**。採用先で以下の版と固有差分を確定した後に「適用済み」へ更新してください。

| 項目 | 内容 |
| --- | --- |
| 配布元・版 | [aidd-project-template](https://github.com/nuitsjp/aidd-project-template) / 版22 |
| 採用元固定コミット | `{{SOURCE_COMMIT}}`（生成に使用した40桁SHAを記載） |
| 設計・文書標準 | [版17](standards/design-and-documentation.md) |
| モック標準 | [版20](standards/mock-driven-development.md) |
| React + .NET 拡張 | 0.1.0 / React・ASP.NET Core・SQLite・並列 E2E の参照実装 |
| 固有差分 | React と ASP.NET Core の責務分担、HTTP JSON と SSE、Microsoft.Data.Sqlite の保存、実処理までの並列 E2E を提供します。具体設計は `design/`、技術固有の構造は `architecture-react-dotnet.md` に置きます |

生成時は `template/` を先にコピーし、`react-dotnet-template/` の内容で上書きします。共通資材（`AGENTS.md`、`docs/standards/`、検査スクリプト、`.agents/skills/usecase-docs/`、`LICENSE`）は共通側から取得し、`docs/project.md` と本書を含む React + .NET 固有文書は拡張側で個別管理します（共通版との部分マージは行いません）。

生成後に配布元の同じ固定コミットから一組で更新するのは `AGENTS.md`、標準 2 件、`scripts/doc_check.py`、`.agents/skills/usecase-docs/` です。その他の文書、実装、設定、依存定義とロック、DB 移行は採用先が管理し、雛形全文とは同期しません。

<a id="mock-scope"></a>
## 2. モック駆動開発の適用範囲

採用先で新設・変更するシナリオへ適用します。参照実装には仕様確認用の固定データを残さず、既定の実行経路は実処理とします。テスト用データと実処理の検証は区別します。

<a id="sources"></a>
## 3. 文書の役割

| 正本 | 内容 |
| --- | --- |
| 本書 | 適用する標準、固有差分、文書の責務 |
| [project.md](project.md) | 目的、制約、ユースケース一覧、外部事実、実行・検証手順 |
| [architecture.md](architecture.md) | 現在のシステム構成、実現パターンの適用条件、設計上の制約 |
| `design/UCP-n.md` | 実現パターンごとの役割、実装パス、シーケンス、結果確定点、モック境界、UC 固有の逸脱 |
| [design/data.md](design/data.md) | 保存方式、テーブル定義、データ制約 |
| [architecture-react-dotnet.md](architecture-react-dotnet.md) | React・ASP.NET Core の責務、HTTP JSON 契約、起動単位、並列 E2E の分離原則 |
| `usecases/<ユースケース名>/README.md` | 主アクター、目的、共通条件、シナリオ一覧、実現パターン |
| `usecases/<ユースケース名>/scenarios/<シナリオ名>.md` | シナリオの条件、手順、受け入れ条件 |
| `../.agents/skills/usecase-docs/` | 文書標準に従う作成手順と雛形 |
| standards/ | 適用する標準の原文 |
| [README.md](../README.md) | プロジェクト概要と参照案内 |
| [AGENTS.md](../AGENTS.md) | 作業時の参照先と注意点 |

既存の正本で扱える事項について管理文書を新設しません。ログ・画像・試験 DB はリポジトリで追跡しません。

<a id="agreements"></a>
## 4. 仕様変更と参照実装の扱い

適用する標準・固有差分・正本の責務と、製品として定義した現在の要件・制約・仕様・完了条件・データ設計を変更する場合は、[変更手続き](standards/design-and-documentation.md#agreement-changes) に従います。

参照実装の仕様は製品の承認済み仕様として扱いません。導入時に製品固有の目的、制約、シナリオ、受け入れ条件を各正本へ定義します。
