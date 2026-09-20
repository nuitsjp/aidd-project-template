# テンプレートの保守

本書は配布元における保守指針です。`template/` 内の初期値や記入欄は配布用であり、本リポジトリ自身の作業状況ではありません。

保守時は技術中立性を維持し、過度な規則や記録負担を避け、モックから実処理への移行影響に配慮します。新規の規則は採用プロジェクトで実際に観測された失敗への対応に限定し、根拠のない規則は追加しません。導入手順を変更した場合はルートの [README.md](README.md) も更新します。

## 共通テンプレートとWails差分の管理

`template/` を技術中立な共通の正本、`wails-template/` をWails固有の差分とする。初期状態はルートの `mise run init:wails <出力先>` で、`template/`、`wails-template/`、ルートの `LICENSE` の順に配置する。同名ファイルはWails側で全体を上書きし、部分マージ・変数展開は行わない。出力先は新規ディレクトリに限定し、既存プロジェクトの更新には使わない。

| 対象 | 管理ルール |
| --- | --- |
| `AGENTS.md`、`docs/standards/`、`scripts/doc_check.py` | `template/` だけで管理し、Wails側に複製・上書き用ファイルを置かない。Wails固有の注意は既存の固有文書に記載する。 |
| `LICENSE` | ルートだけで管理し、生成時に配置する。 |
| `README.md`、`docs/project.md`、`docs/document-policy.md`、`docs/architecture.md`、`docs/usecases/` | Wails側で個別管理する。共通版の記入欄で固有の仕様・合意・検証記録を置き換えない。 |
| Wails固有のコード・設定・`docs/architecture-wails.md` | `wails-template/` で管理し、生成先に配置する。 |

共通側を変更した際は、同一変更内でWails側への影響を確認する。`project.md` は必須項目・文書構造の変更を必要な範囲で手動反映し、製品固有の内容は維持する。`document-policy.md` は生成物に入る配布版・標準の版と採用記録を一致させ、適用範囲・固有差分・正本配置・合意保護が新しい共通規則と整合するか確認する。記録する版は実際に生成へ使用する版とし、作成当初のコミットを現在の採用版の代用にしない。

`wails-template/` 単体では実行・配布・文書検査をしない。生成先で開発・検証し、保守する変更は対応する正本へ反映する。生成物と依存取得物は本リポジトリにコミットしない。生成によってサンプルの仕様合意や未実施の検証が完了したとは扱わない。

## 版の更新

`template/` 内のファイルを変更した場合は、同一変更内で以下を更新します。

- ルート README の「配布版」と `template/docs/document-policy.md` 第1節の「配布元・版」を1つ上げる。
- `template/docs/standards/` の標準を変更した場合は、該当標準の冒頭の版と `document-policy.md` 第1節の該当行を1つ上げ、`python template/scripts/doc_check.py --print-hashes template` の出力で `template/scripts/doc_check.py` の `EXPECTED_HASHES` を更新する（未変更の標準は据え置き）。
- ルート README の変更履歴に、変更ファイル、変更点、標準の版、採用側への影響を追記し、配布物の総行数（Markdown とスクリプト）を併記する。

`template/` を変更しない修正（Wails固有部分、生成タスク、本書やルート README の変更）では共通の配布版を上げません。Wails拡張の版は `wails-template/docs/document-policy.md` で別に管理します。

## 変更時の確認

- `python template/scripts/doc_check.py template` を実行し、全6判定が NG なく通ること（未記入のテンプレートの合意記録は0件でよい）。テーブル設計の合意条件と記録先の整合性は文書レビューで確認する。
- ルート README 第2節の導入手順（PowerShell と bash の両方）を一時ディレクトリで実行し、`template/` の全ファイルと LICENSE が正しく配置されること。
- `template/` 配下の Markdown 合計行数が 410 行以下であること。
- テンプレート全文に、他ユースケースへの横展開を許可する記述（「独立した機能」「先行して進め」等）が含まれていないこと。
- 共通側またはWails差分・生成タスクを変更した場合は、一時ディレクトリへWails初期状態を生成する。隠しファイルを含む配置、Wails側の上書き、共通ファイルとLICENSEの一致、既存出力先の拒否を確認し、生成先で `python scripts/doc_check.py .` がNGなく通ること。採用版・固有文書の整合性もレビューする。
- Wailsの実装を変更した場合は、生成先で `node scripts/run.mjs verify` を実行し、Windows実機での確認範囲と分けて報告する。生成・文書だけの変更ではアプリ全体の検証を必須とせず、未実施の検証を明記する。
