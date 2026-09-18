# エージェント行動指針

1回のセッションで1本の系列を段階1から段階5まで通します。着手前に対象のユースケース・系列と段階を利用者と確認し、自己判断で段階を進めません。初期の記入欄や例示は確定仕様や検証実績ではないため、前提としません。

作業時は下表の正本を参照します。

| 作業 | 参照する正本 |
| --- | --- |
| すべての変更 | [プロジェクト定義](docs/project.md)、[保護する合意](docs/document-policy.md#agreements)、[変更手続き](docs/standards/design-and-documentation.md#agreement-changes) |
| 仕様・設計・実装・テストの作成・変更・レビュー | [設計・実装の原則](docs/standards/design-and-documentation.md#implementation-principles)、対象ユースケースの本文と受け入れ条件 |
| 全体構造・実現パターン・テーブル設計・設計判断の参照と変更 | [アーキテクチャ](docs/architecture.md)、[全体設計と先行してよい成果物](docs/standards/design-and-documentation.md#architecture-method) |
| 層・抽象化・依存関係などの追加 | [仕組みの追加基準](docs/standards/design-and-documentation.md#design-decisions) |
| 系列の追加・変更、次の系列やユースケースへ進む判断 | [適用範囲](docs/document-policy.md#mock-scope)、[モック駆動開発の標準](docs/standards/mock-driven-development.md#workflow)、[ユースケース一覧](docs/project.md#usecases) から参照する本文の合意記録と [検証結果](docs/project.md#verification) |
| 文書の作成・変更・移動・削除 | [文書と記録の基準](docs/standards/design-and-documentation.md#document-roles) |
| 導入、規約・標準・文書方針の改訂 | [文書方針](docs/document-policy.md)、[標準の扱い](docs/standards/design-and-documentation.md) |

## 作業原則

- **ユースケースの検討**: 新規・変更とも、検討した案の全文と論点を応答の本文に提示して停止し、利用者と議論します。要約やファイルへのリンクだけで確認を求めません。修正時も更新した案の全文を提示し、利用者が議論の完了と内容を明示的に確認した後にのみファイルへ反映します。それまではユースケース本文・一覧・関連設計の作成や更新、下書きのファイル保存を行いません。検討依頼や修正指示だけでは書き込みの許可とみなしません。詳細は [モック標準の提示と保存](docs/standards/mock-driven-development.md#discussion) に従います。
- **テストコード**: 対象系列の段階1〜4ではテストコードを作成・変更しません。段階4で実処理の実装を終え、段階5で合意したシナリオを固定する E2E テストを追加・更新・実行してから次の系列へ進みます。既存テストは維持し、具体的な手順は [モック標準第2節](docs/standards/mock-driven-development.md#workflow) に従います。
- **画面確認の準備**: 利用者に画面を通した確認を依頼する場合は、必ずアプリケーションを起動し、確認する画面の URL を提示します。
- **停止点**: 人の確認を待つのは「全体設計の合意」と、系列ごとの段階1（記述の確認）・段階3（動作合意）・段階5（完了判断）、およびテーブルの追加・変更がある場合の段階4開始前（テーブル設計の合意）です。UI 確認を省略しても、必要なテーブル設計の合意は省略しません。停止点では作業を中断して応答を待ち、得られた原文を合意記録に引用します。質問は未確定事項に絞り、合意済み事項の再確認は行いません。合意待ち中の作業は [モック標準第2節](docs/standards/mock-driven-development.md#workflow) に従います。
- **スコープの遵守**: 依頼範囲に必要な作業のみを進め、推測による機能追加や無関係なリファクタリングは行いません。調査・評価の依頼では所見を成果物とし、実装は変更しません。
- **文書管理**: `docs/standards/` は編集しません。文書には現在の状態のみを記録し、経緯は git、証跡はテストや CI に委ねます。規約・標準・文書方針の改訂は実装作業から分離します。
- **完了基準と報告**: 完了前に `scripts/doc_check.py` を実行して出力を報告に含め、[完了基準](docs/standards/design-and-documentation.md#completion) に照らして確認します。未実施の検証は「未検証」と明記し、段階を進めません。エラー発生時は出力をそのまま提示します。
