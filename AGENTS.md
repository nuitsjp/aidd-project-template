# テンプレートの保守

本書は配布元における保守指針です。`template/` 内の初期値や記入欄は配布用であり、本リポジトリ自身の作業状況ではありません。

保守時は技術中立性を維持し、過度な規則や記録負担を避け、モックから実処理への移行影響に配慮します。新規の規則は採用プロジェクトで実際に観測された失敗への対応に限定し、根拠のない規則は追加しません。導入手順を変更した場合はルートの [README.md](README.md) も更新します。

## 版の更新

`template/` 内のファイルを変更した場合は、同一変更内で以下を更新します。

- ルート README の「配布版」と `template/docs/document-policy.md` 第1節の「配布元・版」を1つ上げる。
- `template/docs/standards/` の標準を変更した場合は、該当標準の冒頭の版と `document-policy.md` 第1節の該当行を1つ上げ、`python template/scripts/doc_check.py --print-hashes template` の出力で `template/scripts/doc_check.py` の `EXPECTED_HASHES` を更新する（未変更の標準は据え置き）。
- ルート README の変更履歴に、変更ファイル、変更点、標準の版、採用側への影響を追記し、配布物の総行数（Markdown とスクリプト）を併記する。

`template/` を変更しない修正（本書やルート README のみの変更）では版を上げません。

## 変更時の確認

- `python template/scripts/doc_check.py template` を実行し、判定 1〜3、6、7 が NG なく通ること（未記入のテンプレートでは判定 4・5 は対象外または OK）。
- ルート README 第2節の導入手順（PowerShell と bash の両方）を一時ディレクトリで実行し、`template/` の全ファイルと LICENSE が正しく配置されること。
- `template/` 配下の Markdown 合計行数が 410 行以下であること。
- テンプレート全文に、他ユースケースへの横展開を許可する記述（「独立した機能」「先行して進め」等）が含まれていないこと。
