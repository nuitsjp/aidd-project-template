# データ設計

関連: [アーキテクチャ](../architecture.md)、[UCP-1](UCP-1.md)、[UCP-2](UCP-2.md)。

DB は `DB_PATH` で指定したサーバーの永続領域に配置し、本番・E2E とも同一マイグレーションを使用します。SQL 移行は `backend/Infrastructure/Persistence/Migrations` に置きます。`Microsoft.Data.Sqlite` で操作ごとに接続を開き、WAL、外部キー制約、有限の busy timeout を有効にします。トランザクションは外部 I/O や利用者の確認待ちを含まない短い処理で確定します。

テーブル設計は [data.dbml](data.dbml) に DBML で記載します。論理型と SQLite（STRICT テーブル）の型の対応は次のとおりです。

| DBML の論理型 | SQLite の型 | 用途 |
| --- | --- | --- |
| text | TEXT | 識別子、文字列、UTC の ISO 8601 文字列による日時 |
| integer | INTEGER | 版番号 |

テーブル設計と値の制約は参照実装の仕様であり、製品へ暗黙に引き継ぎません。
