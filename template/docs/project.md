# {{PROJECT_NAME}} のプロジェクト定義

プロジェクト共通の要件・制約、ユースケース一覧、確認した事実、実行手順、および検証結果の正本です。全体構造は [アーキテクチャ](architecture.md) を参照します。現在の状態のみを記録し、作業経緯は含めません。

## 1. 目的と範囲

| 項目 | 内容 |
| --- | --- |
| 解決する問題・達成したい結果 | {{PURPOSE}} |
| 利用者・利用場面 | {{USERS_AND_CONTEXT}} |
| 今回の対象 | {{SCOPE}} |
| 今回の対象外 | {{NON_GOALS}} |

## 2. 制約・品質要求・受け入れ条件

{{CONSTRAINTS_AND_ACCEPTANCE}}

動作環境、規模、費用、データ取扱、セキュリティ、運用上の制約と、客観的に判定可能な受け入れ条件を記録します（判断者・日付・根拠を明記）。判断に必要な未確定事項は推測で埋めず、停止点で利用者に確認して確定した内容のみを記録します。

<a id="usecases"></a>
## 3. ユースケース一覧

ユースケースの本文・シナリオ（主成功系列・拡張系列）・合意記録は `usecases/UC-n.md` に集約し、下表から参照します。案の検討・保存は [提示と保存の手順](standards/mock-driven-development.md#discussion) に従います（未着手の UC は本文を作成せず、リンクも付けません）。

ユースケースの単位・系列の分割・モック適用は [モック標準のユースケース分割](standards/mock-driven-development.md#discussion) に従います。

| UC ID | 主アクター | 目的 | 実装順序 | 実現パターン | モック適用 |
| --- | --- | --- | --- | --- | --- |
| [UC-1](usecases/UC-1.md) | {{ACTOR}} | {{GOAL}} | 1 | [UCP-1](design/UCP-1.md) | 対象 |

<a id="design"></a>
## 4. 確認した事実

{{FACTS}}

- **確認した事実**: 外部仕様や既存コードの調査結果（情報源、対象版、確認日、確認範囲）。仮定と明確に区別します。外部システムの実測応答を保存する場合は `reference/` に配置して参照します。
- **技術選定の根拠**: 言語・ライブラリ・モック機構を評価した事実を記録し、[仕組みの追加基準](standards/design-and-documentation.md#design-decisions) に基づく重要な選定判断はアーキテクチャから参照します。
- **設計への参照**: 全体方針・重要な判断は [アーキテクチャ](architecture.md)、具体的な処理と UC ごとのパターンからの逸脱は一覧から参照する `design/UCP-n.md`、保存形式は [データ設計](design/data.md) に記録します。

<a id="commands"></a>
## 5. 実行・切り替え・検証手順

{{RUN_AND_VERIFY_COMMANDS}}

環境構築、作業ディレクトリ、実行コマンド、設定、成功確認の手順を明記します。モック利用時の切り替えは [モック標準](standards/mock-driven-development.md#boundaries) に従います。

<a id="verification"></a>
## 6. 検証結果

| UC・系列 ID | 段階 | 構成 | 実行日 | コマンド | 合否 | 対象コミットまたは CI 参照 |
| --- | --- | --- | --- | --- | --- | --- |

- 検証結果の記録は [設計標準の検証結果の記録](standards/design-and-documentation.md#verification-records) に従います。段階は [モック標準](standards/mock-driven-development.md#workflow) の番号（1〜6）を記載します。
