# エージェント行動指針

着手前に [文書方針](docs/document-policy.md) の採用状態・適用範囲と [PLAN.md](PLAN.md) を確認してください。導入前の記入欄や例を、確定した仕様や検証実績として扱わないでください。

| 作業 | 参照する正本 |
| --- | --- |
| すべての変更 | [プロジェクト定義](docs/project.md) の関連箇所、[保護する合意](docs/document-policy.md#agreements)、[変更手続き](docs/standards/design-and-documentation.md#agreement-changes) |
| 設計・実装・テストの作成、変更、レビュー | [設計と文書の標準](docs/standards/design-and-documentation.md)、対象機能の仕様・設計・検証条件 |
| モック対象となる機能の変更 | [適用範囲](docs/document-policy.md#mock-scope)、[モック駆動開発の標準](docs/standards/mock-driven-development.md) |
| 文書の作成、変更、移動、削除 | 文書方針と [文書の追加基準](docs/standards/design-and-documentation.md#document-roles) |

依頼された範囲で必要な作業を進め、推測による機能追加や無関係な整理を行わないでください。調査や評価だけの依頼では所見を成果物とし、実装は変更しません。

判断にユーザー入力が必要な場合は、手元で確認できる事実と変更内容を具体化してから、未確定の点だけを確認してください。既に明示された合意の再承認は不要です。未決事項に依存しない作業は継続します。

完了前に、依頼と合意内容に対する差分、必要な検証の結果、正本間のリンク、未決・未検証事項の状態を確認してください。結果は実行した確認に基づいて報告し、失敗した検証は出力とともに、未実施の検証は未実施と明記してください。
