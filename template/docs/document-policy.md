# 文書方針

本書は、標準の採用記録、モック駆動開発の適用範囲、正本の配置、および保護する合意を定義します。

<a id="adoption"></a>
## 1. 採用記録

導入状態: **未適用**。各項目を確定後に「適用済み」へ更新してください。

| 項目 | 内容 |
| --- | --- |
| 配布元・版 | [aidd-project-template](https://github.com/nuitsjp/aidd-project-template) / 版5 |
| 設計・文書標準 | [project-template-design-and-documentation / 版4](standards/design-and-documentation.md) |
| モック標準 | [project-template-mock-driven-development / 版4](standards/mock-driven-development.md) |
| 採用日・判断者・合意の根拠 | {{ADOPTION_RECORD}} |
| プロジェクト固有の差分と理由 | {{LOCAL_RULE_DIFFERENCES}} |

採用後、設計・文書標準はすべての変更に適用されます。モック標準の適用範囲は第2節で定めます。`standards/` の本文は編集せず、プロジェクト固有の差分は上表に理由とともに記録します（差分がない場合は「なし」と明記）。テンプレートの新版は自動適用しません。`scripts/doc_check.py` を実行できない構成では、その旨も差分に記録します。

<a id="mock-scope"></a>
## 2. モック駆動開発の適用範囲

ユースケースの主成功系列または拡張系列を新設・変更する作業に適用します。仕様文面を変更しない作業（確定済み仕様の不具合修正、振る舞いを変えない内部変更、文書修正）は対象外です（単体テスト等でのスタブ・モック利用とは区別します）。

プロジェクト固有の除外とその理由: {{MOCK_SCOPE}}

<a id="sources"></a>
## 3. 文書の役割と分割

| 正本 | 扱う内容 |
| --- | --- |
| 本書 | 標準の採用記録、モック適用範囲、正本の配置、保護する合意 |
| `standards/` 各標準 | プロジェクト非依存の開発・文書基準（配布元からの輸入物） |
| [project.md](project.md) | プロジェクトの目的・制約、ユースケースと合意記録、確認した事実、手順、検証結果 |
| [architecture.md](architecture.md) | 全体設計の合意、システム構成、実現パターン、設計判断 |
| `reference/` | 外部システムから取得した実測応答（取得日時・方法・対象版を記録。利用時のみ） |
| [PLAN.md](../PLAN.md) | 現在地、未決事項、ユースケース進捗、再開情報 |
| [README.md](../README.md) | プロジェクト概要と参照案内 |
| [AGENTS.md](../AGENTS.md) | AIエージェントの作業規範 |

- **新設の禁止**: 本表にない規約・方針・プロセス文書は新設しません。固有の規則は第1節の差分欄、[project.md](project.md) 第2節の制約、または実現パターン内に記述します。
- **文書の分割基準**: 単独参照の必要性や更新頻度の違いにより管理が困難になった場合のみ分割を認めます。分割時は元の記述を参照リンクへ置き換え、本表と関連リンクを更新します。
- **図の形式**: 図は Mermaid で記述します。システムコンテキストとコンテナは flowchart または C4 構文、系列は sequenceDiagram を使用します。

<a id="agreements"></a>
## 4. 保護する合意

採用規則、適用範囲、正本の責務、[プロジェクト定義](project.md) で合意済みの要件・制約・仕様・完了条件、および [アーキテクチャ](architecture.md) の全体設計合意欄と設計判断 ID を保護対象とします。これらを変更・緩和する場合は [変更手続き](standards/design-and-documentation.md#agreement-changes) に従います。設計判断は、実装の破棄や文書再編時も維持します。

テンプレート初期の記入欄、検討中の案、モック上の仮定は合意とみなさず、未確定事項は [PLAN.md](../PLAN.md) で管理します。
