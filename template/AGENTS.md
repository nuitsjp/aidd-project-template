# エージェント行動指針

1回のセッションで1本の系列を段階1から段階5まで通します。着手前に対象のユースケース・系列と段階を利用者と確認し、自己判断で段階を進めません。初期の記入欄や例示は確定仕様や検証実績ではないため、前提としません。

作業時は下表の正本を参照します。

| 作業 | 参照する正本 |
| --- | --- |
| すべての変更 | [プロジェクト定義](docs/project.md)、[保護する合意](docs/document-policy.md#agreements)、[変更手続き](docs/standards/design-and-documentation.md#agreement-changes) |
| 仕様・設計・実装・テストの作成・変更・レビュー | [設計・実装の原則](docs/standards/design-and-documentation.md#implementation-principles)、対象ユースケースの本文と受け入れ条件 |
| 全体構造・実現パターン・テーブル設計・設計判断の参照と変更 | [アーキテクチャ](docs/architecture.md)、[全体設計と先行してよい成果物](docs/standards/design-and-documentation.md#architecture-method) |
| 層・抽象化・依存関係などの追加 | [仕組みの追加基準](docs/standards/design-and-documentation.md#design-decisions) |
| 系列の追加・変更、次の系列やユースケースへ進む判断 | [適用範囲](docs/document-policy.md#mock-scope)、[モック駆動開発の標準](docs/standards/mock-driven-development.md#workflow)、[プロジェクト定義](docs/project.md#usecases) の合意記録と検証結果 |
| 文書の作成・変更・移動・削除 | [文書と記録の基準](docs/standards/design-and-documentation.md#document-roles) |
| 導入、規約・標準・文書方針の改訂 | [文書方針](docs/document-policy.md)、[標準の扱い](docs/standards/design-and-documentation.md) |

## 作業原則

- **停止点**: 人の確認を待つのは「全体設計の合意」と、系列ごとの段階1（記述の確認）・段階3（動作合意）・段階5（完了判断）、およびテーブルの追加・変更がある場合の段階4開始前（テーブル設計の合意）です。UI 確認を省略しても、必要なテーブル設計の合意は省略しません。停止点では作業を中断して応答を待ち、得られた原文を合意記録に引用します。質問は未確定事項に絞り、合意済み事項の再確認は行いません。合意待ち中の作業は [モック標準第2節](docs/standards/mock-driven-development.md#workflow) に従います。
- **スコープの遵守**: 依頼範囲に必要な作業のみを進め、推測による機能追加や無関係なリファクタリングは行いません。調査・評価の依頼では所見を成果物とし、実装は変更しません。
- **文書管理**: `docs/standards/` は編集しません。文書には現在の状態のみを記録し、経緯は git、証跡はテストや CI に委ねます。規約・標準・文書方針の改訂は実装作業から分離します。
- **完了基準と報告**: 完了前に `scripts/doc_check.py` を実行して出力を報告に含め、[完了基準](docs/standards/design-and-documentation.md#completion) に照らして確認します。未実施の検証は「未検証」と明記し、段階を進めません。エラー発生時は出力をそのまま提示します。
