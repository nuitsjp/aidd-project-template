# UC-1. {{USE_CASE_NAME}}

- 主アクターと目的: {{ACTOR_AND_GOAL}}（[プロジェクトの目的と範囲](../project.md) との対応）
- 前提（任意）: {{PRECONDITION}}
- 主成功系列（UC-1-M、UI 確認: 要）:

| 手順 | アクター | 操作・処理 |
| --- | --- | --- |
| 1 | {{ACTOR}} | {{STEP}} |

- 拡張（系列 ID の末尾は -X1 から。着手した系列のみ記載）:

| 系列 ID | 分岐点 | 条件 | 動作 | UI 確認 | 理由 |
| --- | --- | --- | --- | --- | --- |

- 受け入れ条件: {{ACCEPTANCE}}
- 実現パターンと逸脱: [UCP-1](../design/UCP-1.md)（逸脱がある場合も参照先に記録）
- 合意記録（系列ごとに1件。段階3、UI 確認不要の系列は段階1確認・本文保存後に記録。「提示コミット」は [モック標準](../standards/mock-driven-development.md#discussion) に従う）:
  - UC-1-M / 提示コミット: {{COMMIT_HASH}} / 論点と回答: {{DECISIONS}}
    > {{USER_RESPONSE}}
- 完成系監査記録（段階5で利用者の承認後に系列ごと1件）:
  - UC-1-M / 提示コミット: {{AUDIT_COMMIT_HASH}}
    > {{AUDIT_USER_RESPONSE}}

合意記録と完成系監査記録は [モック標準の記録書式](../standards/mock-driven-development.md#discussion) に従います。合意記録と必要なテーブル設計の合意が揃うまで実処理接続へ進めず、完成系監査記録が揃うまで段階6の E2E テスト実装へ進めません。
