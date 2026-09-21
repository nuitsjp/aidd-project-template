# 文書方針

<a id="adoption"></a>
## 1. 採用記録

| 項目 | 内容 |
| --- | --- |
| 共通資材の採用元 | 同一チェックアウトの `template/` を生成時に配置（配布版19） |
| 採用元固定コミット | {{SOURCE_COMMIT}}（正式採用時に生成元の40桁SHAを記録） |
| 作成時の根拠 | [aidd-project-template](https://github.com/nuitsjp/aidd-project-template) / 版13 / `88b31b40c65a2ce35201eeda34358e2d103bac23`（作成時点の確認値） |
| React拡張 | 0.2.3 / Node版固定、Playwright headless shell の導入と依存ロックの同梱 |
| 設計・文書標準 | [版14](standards/design-and-documentation.md) 原文維持 |
| モック標準 | [版17](standards/mock-driven-development.md) 原文維持 |
| 作成依頼 | 2026-09-20、利用者による「E2EテストでユーザーからDBの更新まで含めたテストを並列で実行することを加味して、アーキテクチャを設計してください。その上でWails同様にダウンロードできる形で提供してください」 |
| 固有差分 | サンプル仕様・実装・テストを一式として提供。系列ごとの UI 合意と完成系監査は未実施であり、同梱 E2E を標準の段階6完了とは扱いません。共通構造の補足を architecture-react.md へ分離しています |

生成時は `template/` を先にコピーし、`react-template/` の内容で上書きします。共通資材（`AGENTS.md`、`docs/standards/`、検査スクリプト、`LICENSE`）は共通側から取得し、`docs/project.md` と本書は React 差分側で個別管理します（共通版との部分マージは行いません）。

生成後に配布元から一組で更新するのは `AGENTS.md`・標準2件・`scripts/doc_check.py` です。同じ固定コミットから取得し、固有規則は本書の差分欄に記録します。その他の文書・実装・設定・依存定義とロック・DB移行履歴は採用先が管理し、雛形全文は同期しません。書式変更が必要な版だけ移行手順を適用し、検査後に採用版と採用元固定コミットを更新します。

<a id="mock-scope"></a>
## 2. モック駆動開発の適用範囲

採用先で新設・変更する系列へ適用します。段階4完了後の本参照実装には合意用固定データを残していません。テストデータの用意や既存試験の並列実行によって系列の開発順序を変更することはありません。

<a id="sources"></a>
## 3. 文書の役割

| 正本 | 内容 |
| --- | --- |
| 本書 | 採用範囲・差分・合意の扱い |
| [project.md](project.md) | 目的、UC一覧、運用手順、検証結果 |
| [architecture.md](architecture.md) | サンプルの構成、実現パターン、テーブル、結果確定点 |
| [architecture-react.md](architecture-react.md) | 共通の責務と依存方向、並列E2Eの分離原則 |
| usecases/UC-n.md | 系列、受け入れ条件、合意状態 |
| standards/ | 輸入した標準の原文 |
| [README.md](../README.md) | 起動・検証の案内 |
| [AGENTS.md](../AGENTS.md) | AI作業規範 |

既存の正本で対応可能な事項について管理文書を新設しません。ログ・画像・試験 DB はリポジトリで追跡しません。

<a id="agreements"></a>
## 4. 保護する合意

対話と機能の n:n 対応、SQLite 既定、実処理までの並列 E2E、過剰設計・過剰文書の抑止を維持します。サンプルの画面や運用認証方式は採用先へ自動継承せず、未実施の合意や架空コミットは記録しません。
