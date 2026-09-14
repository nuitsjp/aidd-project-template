# テンプレートの保守

本書は配布元の保守指針です。プロジェクトへの採用時は、`template/` の内容と [LICENSE](LICENSE) をコピーして利用します。

本リポジトリは特定の技術構成やツールに依存しない開発テンプレートを提供します。`template/` 内の初期状態や記入欄は配布用の初期値であり、本リポジトリ自身の作業状況として扱わないでください。

保守や変更の際は、技術中立性を保ち、規則や記録の負担が増大しないこと、およびモックから実処理への移行影響に配慮してください。規則を追加するときは、採用プロジェクトで観測された失敗に対応させ、対応する観測がない規則は追加しません。導入手順を変更した場合は、ルートの [README.md](README.md) もあわせて更新します。

## 版の更新

`template/` 内のファイルを変更したら、同じ変更の中で次を更新します。

- ルート README の「配布版」と `template/docs/document-policy.md` 第1節の「配布元・版」を1つ上げる。
- `template/docs/standards/` の標準を変更した場合は、その標準の冒頭の版と `document-policy.md` 第1節の該当行を1つ上げ、`python template/scripts/doc_check.py --print-hashes template` の出力で `scripts/doc_check.py` の `EXPECTED_HASHES` を更新する。変更していない標準の版は据え置く。
- ルート README の変更履歴に、変更したファイル、変更点、標準の版、採用側への影響を追記し、配布物の総行数（Markdown とスクリプト）を併記する。

`template/` を変更しない修正（本書やルート README のみの変更）では版を上げません。

## 変更時の確認

- `python template/scripts/doc_check.py template` を実行し、判定 1〜3、6、7 が NG なく通ること（テンプレートの記入欄は未記入なので、判定 4・5 は「対象なし」または OK になる）。
- ルート README 第2節の導入手順（PowerShell と bash の両方）を一時ディレクトリーで実行し、`template/` の全ファイルと LICENSE が配置されること。
- `template/` の Markdown 合計が 410 行以下であること。
- テンプレート全文に、他のユースケースへの横展開を許可する記述（「独立した機能」「先行して進め」）が残っていないこと。
