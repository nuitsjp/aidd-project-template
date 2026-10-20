# {{PROJECT_NAME}} のプロジェクト定義

プロジェクト共通の要件・制約、ユースケース一覧、確認した事実、および実行・検証手順の正本です。全体構造は [アーキテクチャ](architecture.md) を参照します。

## 1. 目的と範囲

| 項目 | 内容 |
| --- | --- |
| 解決する問題・達成したい結果 | {{PURPOSE}} |
| 利用者・利用場面 | {{USERS_AND_CONTEXT}} |
| 今回の対象 | {{SCOPE}} |
| 今回の対象外 | {{NON_GOALS}} |

## 2. 制約・品質要求・受け入れ条件

{{CONSTRAINTS_AND_ACCEPTANCE}}

動作環境、規模、費用、データ取扱、セキュリティ、運用上の制約と、客観的に判定可能な受け入れ条件を記述します。

<a id="usecases"></a>
## 3. ユースケース一覧

ユースケースの本文は `usecases/<名称>/` に置き、文書構造は [作成用スキル](../.agents/skills/usecase-docs/SKILL.md)、単位・分割・モック適用と案の記載・確認は [モック標準](standards/mock-driven-development.md#discussion) に従います。未着手のユースケースは下表に名称だけを置きます。

| ユースケース | 主アクター | 目的 | 実装順序 | 実現パターン | モック適用 |
| --- | --- | --- | --- | --- | --- |
| {{USE_CASE_NAME}} | {{ACTOR}} | {{GOAL}} | 1 | [UCP-1](design/UCP-1.md) | 対象 |

<a id="design"></a>
## 4. 確認した事実

{{EXTERNAL_FACTS}}

外部仕様や既存コードの調査結果を [記録内容](standards/design-and-documentation.md#document-roles) に従って記載します。外部システムの実測応答を保存する場合は `reference/` に配置して参照します。

<a id="commands"></a>
## 5. 実行・切り替え・検証手順

{{RUN_AND_VERIFY_COMMANDS}}

環境構築、作業ディレクトリ、実行コマンド、設定、期待結果と、自動テスト・実機確認の対象と条件を記載し、最新コードで再実行できる手順を維持します。モック利用時の記載事項は [モック標準](standards/mock-driven-development.md#workflow) に従います。
