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
| Wails拡張 | 0.2.6 / Wails・Go・React による Windows デスクトップ参照実装 |
| 固有差分 | Wails のライフサイクル、Go Service、単一 JSON 保存、NSIS 更新を提供します。共通構造の補足は architecture-wails.md、実現パターンは `design/`、保存方式は `design/data.md` に置きます |

生成時は `template/` を先にコピーし、`wails-template/` の内容で上書きします。共通資材（`AGENTS.md`、`docs/standards/`、検査スクリプト、`.agents/skills/usecase-docs/`、`LICENSE`）は共通側から取得し、`docs/project.md` と本書は Wails 差分側で個別管理します（共通版との部分マージは行いません）。

生成後は `AGENTS.md`・標準2件・`scripts/doc_check.py`・`.agents/skills/usecase-docs/` を同じ固定コミットから一組で更新します。その他の文書・実装・設定・依存ロックは採用先で管理し、雛形全文とは同期しません。

<a id="mock-scope"></a>
## 2. モック駆動開発の適用範囲

採用先で新設・変更するシナリオへ適用します。配布物の `frontend/tests/fixtures/notes.ts` は合成点の機構を示す試験用データであり、実処理を置き換えるものではありません。採用先で確認用の固定データを置く場合は [Wails補足第4節](architecture-wails.md#4-モックと検証境界) の4箇所を揃え、実処理へ切り替えた後に削除または移動します。試験用データの結果と実処理の検証は区別します。

<a id="sources"></a>
## 3. 文書の役割

| 正本 | 内容 |
| --- | --- |
| 本書 | 適用する標準、固有差分、文書の責務 |
| [project.md](project.md) | 目的、制約、ユースケース一覧、外部事実、実行手順 |
| `usecases/<ユースケース名>/README.md` | 主アクター、目的、共通条件、シナリオ一覧、実現パターン |
| `usecases/<ユースケース名>/scenarios/<シナリオ名>.md` | シナリオの条件、手順、受け入れ条件 |
| `../.agents/skills/usecase-docs/` | 文書標準に従う作成手順と雛形 |
| [architecture.md](architecture.md) | 現在のシステム構成、実現パターンの適用条件、設計上の制約 |
| `design/UCP-n.md` | 実現パターンごとの役割、実装パス、シーケンス、結果確定点、モック境界 |
| [design/data.md](design/data.md) | データ定義・制約、保存方式 |
| [architecture-wails.md](architecture-wails.md) | Wails 共通構造の補足。プロダクト固有の仕様は転記しない |
| `standards/` | 輸入した開発・文書基準 |
| [README.md](../README.md) | 起動・参照案内 |
| [AGENTS.md](../AGENTS.md) | 作業時の参照先と注意点 |

既存の正本で扱える事項について文書を新設しません。ログ・画像・試験データはリポジトリで追跡しません。

<a id="agreements"></a>
## 4. 仕様変更と参照実装の扱い

適用する標準・固有差分・正本の責務と、製品として定義した現在の要件・制約・仕様・完了条件・保存設計を変更する場合は、[変更手続き](standards/design-and-documentation.md#agreement-changes) に従います。変更後は該当する正本を更新し、別文書へ同じ本文を転記しません。

参照実装の仕様は製品の承認済み仕様として扱いません。導入時に製品固有の目的、制約、シナリオ、受け入れ条件を各正本へ定義します。過去の判断・応答・検証結果を文書へ保存せず、現在の仕様と再実行できる手順だけを残します。
