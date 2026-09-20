# エージェント行動指針

変更前に [プロジェクト定義](docs/project.md)、対象UC、[文書方針](docs/document-policy.md)を読む。構造の変更は [アーキテクチャ](docs/architecture.md)と[Wails補足](docs/architecture-wails.md)を参照する。

設計・実装・文書化は [設計標準](docs/standards/design-and-documentation.md)、系列の追加・変更は [モック標準](docs/standards/mock-driven-development.md)に従う。仕様検討の段階で勝手に保存・実装せず、停止点を守る。`docs/standards/`と生成バインディングは編集しない。

この配布物は参照実装であり、サンプルの画面動作・受け入れ条件は利用者確認前である。コードの存在を仕様合意や実機検証とみなさない。採用先の製品に、サンプルの合意や検証を転用しない。

新しい層・共通部品・依存・規約を、将来役立つという理由だけで追加しない。機能アクセス以外からGoの操作を呼ばない。対話をGoへ移さず、保存の整合性をフロントエンドへ移さない。

実装の後に対象系列のテストを追加・更新する。完了報告では `node scripts/run.mjs verify` の結果と、Windows実機で確認した範囲を分ける。文書だけの変更でも `python scripts/doc_check.py .` を実行する。失敗・未実施はそのまま記録する。
