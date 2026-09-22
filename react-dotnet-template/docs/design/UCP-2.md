# UCP-2. 入力・確認・一括確定

関連: [アーキテクチャ](../architecture.md)、[ユースケース](../usecases/複数のメモを確認して一括登録する/README.md)、[データ設計](data.md)。

適用: 複数画面の対話で内容を確認後、関連更新を一体で確定する。

| 役割 | 責務 | 実装パス |
| --- | --- | --- |
| 対話の親 | 段階をまたぐ入力の保持 | `frontend/src/usecases/import-notes/ImportDialogue.tsx` |
| 入力・確認 | プレビューと最終実行 | `frontend/src/usecases/import-notes/ImportInput.tsx`、`ImportConfirm.tsx` |
| 共通のメモ機能 | 検証と一括 INSERT | `backend/features/notes/NotesService.cs` |

```mermaid
sequenceDiagram
  actor U as 利用者
  participant D as 対話
  participant H as HTTP JSON
  participant S as NotesService
  participant DB as SQLite
  U->>D: 複数タイトルを入力
  D->>H: POST /api/notes/preview
  H->>S: 入力と利用者を渡す
  S-->>H: 検証済みの確認内容
  H-->>D: プレビュー JSON
  D-->>U: 確認画面
  U->>D: 一括登録を指示
  D->>H: POST /api/notes/import
  H->>S: 元の入力を再送
  S->>DB: 再検証・BEGIN・全件 INSERT・COMMIT
  S-->>H: 確定件数
  H-->>D: 登録結果
  D-->>U: 登録完了
```

確認待ちはトランザクション外で行います。既存タイトル衝突等で途中失敗した場合は全件をロールバックし、確認内容と下書きを維持します。モック境界と合成点は [アーキテクチャ](../architecture.md) の UI→API の定義に従います。「複数のメモを確認して一括登録する」固有の件数上限は [プロジェクト定義の制約](../project.md#constraints) を参照します。
