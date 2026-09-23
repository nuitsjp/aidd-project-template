# テンプレートの保守

本書は配布元における保守指針です。`template/` 内の初期値や記入欄は配布用であり、本リポジトリ自身の作業状況ではありません。

保守時は技術中立性を維持し、過度な規則や記録負担を避け、モックから実処理への移行影響に配慮します。新規の規則は採用プロジェクトで実際に観測された失敗への対応に限定し、根拠のない規則は追加しません。導入手順を変更した場合はルートの [README.md](README.md) も更新します。

## 共通テンプレートと拡張差分の管理

`template/` を技術中立な共通正本、`wails-template/`・`react-template/`・`react-dotnet-template/` を各技術固有の差分とします。初期状態はルートの `mise run init:wails <出力先>`、`mise run init:react <出力先>` または `mise run init:react-dotnet <出力先>` で、`template/`、選択した拡張ディレクトリ、ルートの `LICENSE` の順に配置します。同名ファイルは拡張側で全体を上書きし、部分マージや変数展開は行いません（新規ディレクトリへの出力のみを対象とします）。

| 対象 | 管理ルール |
| --- | --- |
| `AGENTS.md`、`docs/standards/`、`scripts/doc_check.py`、`.agents/skills/usecase-docs/` | `template/` だけで管理し、拡張側に上書き用ファイルを置かない。採用先では同じ固定コミットから一組で更新する。技術固有の注意は既存の固有文書に記載する。 |
| `LICENSE` | ルートだけで管理し、生成時に配置する。 |
| `README.md`、`docs/project.md`、`docs/document-policy.md`、`docs/architecture.md`、`docs/design/`、`docs/usecases/` | Wails・React の固有文書は各拡張側で個別管理する。React・.NET の製品用ルート文書は共通版を使い、メモの仕様・設計は `react-dotnet-template/reference/docs/` に置く。 |
| 技術固有のコード・設定・`docs/architecture-wails.md`・`docs/architecture-react.md` | 対応する拡張ディレクトリで管理し、生成先に配置する。React・.NET の参照アプリは `react-dotnet-template/reference/` に一式を置く。 |

共通側を変更した際は、同一変更内で各拡張側への影響を確認します。`project.md` は必須項目・文書構造の変更を必要最小限で反映し、固有内容は維持します。`document-policy.md` は生成物に入る配布版・標準版と適用版を一致させ、適用範囲・固有差分・正本配置・仕様変更の扱いが新しい共通規則と整合するか確認します（記載する版は実際に生成へ使用する版とし、過去のコミットを現在の適用版の代用にしません）。

`wails-template/` と `react-template/` は拡張ディレクトリ単体では実行・配布・文書検査を行わず、生成先で開発・検証して保守変更を正本へ反映します。`react-dotnet-template/` のルート `mise.toml` は実プロジェクト用で、現時点のタスクはツール導入と製品文書検査です。参照アプリの全タスクは `reference/mise.toml` に置き、source checkout と生成先の両方で `reference/` から実行します。Visual Studio の F5 は `reference/App.slnx` の App を使用します。source の各 `check:docs` は共通の `template/` と拡張側を一時生成先へ配置し、それぞれ製品文書と参照文書を検査します。アプリの build・test は `reference/` で実行します。source の package はルートの `LICENSE` を使います。共通の文書・検査資材を拡張側へ複製せず、採用先では生成先の `scripts/doc_check.py` と `LICENSE` を使います。生成物や依存取得物はコミットしません。生成によって参照実装の仕様合意や未実施の検証が完了したとは扱いません。

生成後のプロジェクト文書・実装・設定・DB移行履歴は採用先が管理し、初期雛形との全文同期は行いません。継続更新する規則は既存標準に集約し、必要な書式移行は変更履歴に記載します。依存ロックは再現に必要な配布資材として、生成先で検証したものを正本に保存します。

## 版の更新

`template/` 内のファイルを変更した場合は、同一変更内で以下を更新します。

- ルート README の「配布版」と `template/docs/document-policy.md` 第1節の「配布元・版」を1つ上げる。
- `template/docs/standards/` の標準を変更した場合は、該当標準の冒頭の版と `document-policy.md` 第1節の該当行を1つ上げ、`python template/scripts/doc_check.py --print-hashes template` の出力で `template/scripts/doc_check.py` の `EXPECTED_HASHES` を更新する（未変更の標準は据え置き）。
- ルート README の変更履歴に、変更ファイル、変更点、標準の版、採用側への影響を追記し、配布物の総行数（Markdown とスクリプト）を併記する。

`template/` を変更しない修正（拡張固有部分、生成タスク、本書やルート README の変更）では共通の配布版を上げません。各拡張の版は対応する `docs/document-policy.md` で個別に管理します。

## 変更時の確認

- `python template/scripts/doc_check.py template` を実行し、全6判定が NG なく通ること。決定履歴・承認記録・検証表がなく、現在の仕様と確認手順が正本に集約されていることをレビューする。テーブル設計などの利用者確認は会話で行い、静的検査で承認済みとは判定しない。
- ルート README 第2節の導入手順（PowerShell と bash の両方）を一時ディレクトリで実行し、`template/` の全ファイルと LICENSE が正しく配置されること。
- `template/` 配下の Markdown 合計行数が 410 行以下であること。
- テンプレート全文に、他ユースケースへの横展開を許可する記述（「独立した機能」「先行して進め」等）が含まれていないこと。
- 共通側や共有生成処理を変更した場合は全拡張、拡張固有の差分を変更した場合は対象拡張の初期状態を一時ディレクトリへ生成します。隠しファイルを含む配置、拡張側の上書き、共通ファイルと LICENSE の一致、既存出力先の拒否を確認し、生成先で `python scripts/doc_check.py .` が NG なく通ることを確認します。採用版と固有文書の整合性もレビューします。
- 拡張の実装を変更した場合は、生成先で Wails は `node scripts/run.mjs verify`、React は `npm run verify`、React・.NET は source または生成先の `reference/` で `mise run verify` を実行し、実機確認の範囲と分けて報告します。共通側や生成内容・共通継承の確認は、引き続き生成先で行います（文書・生成のみの変更ではアプリ全体の検証を必須とせず、未実施の検証を明記）。
