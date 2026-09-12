# エージェント行動指針

作業着手前に [採用状態・差分](docs/document-policy.md#adoption)、[モックの適用範囲](docs/document-policy.md#mock-scope)、[PLAN.md](PLAN.md) を確認してください。テンプレート初期の記入欄や例を、確定仕様や検証実績として扱ってはなりません。

作業時は下表の正本を参照します。

| 作業 | 参照する正本 |
| --- | --- |
| すべての変更 | [プロジェクト定義](docs/project.md)、[保護する合意](docs/document-policy.md#agreements)、[変更手続き](docs/standards/design-and-documentation.md#agreement-changes) |
| 仕様・設計・実装・テストの作成・変更・レビュー | [設計・実装の原則](docs/standards/design-and-documentation.md#implementation-principles)、対象機能の仕様・設計・検証条件 |
| 層・抽象化・依存関係などの追加 | [仕組みの追加基準](docs/standards/design-and-documentation.md#design-decisions) |
| モック対象となる機能の変更 | [適用範囲](docs/document-policy.md#mock-scope)、[モック駆動開発の標準](docs/standards/mock-driven-development.md) |
| 文書の作成・変更・移動・削除 | [文書と記録の基準](docs/standards/design-and-documentation.md#document-roles) |
| 導入、規約・正本配置の変更 | [文書方針](docs/document-policy.md) |

### 作業原則

- **スコープの遵守**: 依頼範囲に必要な作業のみを進め、推測による機能追加や無関係なリファクタリングを行わないでください。調査や評価の依頼では所見を成果物とし、実装は変更しません。
- **確認と自律進行**: ユーザー確認が必要な場合は、確認済みの事実と論点を整理した上で未確定事項のみを質問してください。合意済み事項の再承認は求めず、未決事項に依存しない作業は先行して進めます。
- **完了基準と報告**: 完了前に [完了基準](docs/standards/design-and-documentation.md#completion) に照らし、依頼との差分、検証結果、リンク整合性、未決事項の状態を確認してください。結果は事実に基づき報告し、失敗時は出力を提示し、未実施の検証は「未実施」と明記します。
