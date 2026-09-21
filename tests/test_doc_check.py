"""doc_check.py の禁止記録・重複本文判定の回帰テスト。"""

from __future__ import annotations

import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


REPOSITORY_ROOT = Path(__file__).resolve().parents[1]
TEMPLATE_ROOT = REPOSITORY_ROOT / "template"
CHECKER = TEMPLATE_ROOT / "scripts" / "doc_check.py"


class DocCheckRegressionTests(unittest.TestCase):
    """各ケースを独立した template コピーへ適用して CLI を実行する。"""

    def run_checker(self, change=None):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory) / "project"
            shutil.copytree(TEMPLATE_ROOT, root)
            if change is not None:
                change(root)
            completed = subprocess.run(
                [sys.executable, "-B", str(CHECKER), str(root)],
                cwd=REPOSITORY_ROOT,
                capture_output=True,
                text=True,
                encoding="utf-8",
                check=False,
            )
            return completed.returncode, completed.stdout + completed.stderr

    def assert_checker_ok(self, change=None):
        returncode, output = self.run_checker(change)
        self.assertEqual(0, returncode, output)
        self.assertIn("NG 0 件", output)
        return output

    def assert_checker_ng(self, change, *needles):
        returncode, output = self.run_checker(change)
        self.assertEqual(1, returncode, output)
        self.assertIn("NG", output)
        for needle in needles:
            self.assertIn(needle, output)
        return output

    @staticmethod
    def write_utf8(path, text):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(text.encode("utf-8"))

    @classmethod
    def append(cls, root, relative, text):
        path = root / relative
        cls.write_utf8(path, path.read_text(encoding="utf-8").rstrip() + "\n\n" + text + "\n")

    @classmethod
    def write_extra(cls, root, relative, text):
        cls.write_utf8(root / relative, text.rstrip() + "\n")

    def test_template_is_clean(self):
        self.assert_checker_ok()

    def test_adr_directory_and_typical_decision_file_are_rejected(self):
        def change(root):
            self.write_extra(root, "docs/adr/ADR-1.md", "# 旧記録\n\n背景と決定を保存していた本文です。")

        self.assert_checker_ng(change, "ADR/決定記録", "docs/adr/ADR-1.md")

    def test_adr_table_and_id_are_rejected(self):
        def change(root):
            self.append(
                root,
                "docs/architecture.md",
                "| ADR ID | 決定 | 理由 |\n| --- | --- | --- |\n| ADR-1 | 採用 | 旧決定 |",
            )

        self.assert_checker_ng(change, "ADR/決定記録", "ADR-1")

    def test_old_headings_and_fill_labels_are_rejected(self):
        def change(root):
            self.append(
                root,
                "docs/project.md",
                "## 決定履歴\n\n- 承認原文: 旧記録\n- 提示コミット: abcdef0\n- 論点と回答: 旧記録",
            )

        self.assert_checker_ng(change, "旧記録の見出し", "旧記録の記入ラベル")

    def test_old_response_and_post_save_notification_labels_are_rejected(self):
        def change(root):
            self.append(
                root,
                "docs/architecture.md",
                "- 応答の原文: 旧記録\n- 利用者の応答原文: 旧記録\n"
                "- 保存後通知の改訂合意: 旧記録\n- 保存後通知の完成系監査記録: 旧記録",
            )

        self.assert_checker_ng(change, "旧記録の記入ラベル")

    def test_old_record_samples_in_quote_and_fence_are_rejected(self):
        def change(root):
            self.append(
                root,
                "README.md",
                "> ## 合意記録\n> - 完成系監査中の変更: 修正\n\n```markdown\n## 検証結果\n- 承認原文: 旧記録\n```",
            )

        self.assert_checker_ng(change, "旧記録の見出し", "旧記録の記入ラベル")

    def test_verification_table_is_rejected_without_stage_column(self):
        def change(root):
            self.write_extra(
                root,
                "docs/old-verification.md",
                "| 構成 | 実行日 | 合否 | 対象コミットまたは CI 参照 |\n"
                "| --- | --- | --- | --- |\n"
                "| 本番 | 2026-09-21 | 合格 | abcdef0 |",
            )

        self.assert_checker_ng(change, "旧検証表", "docs/old-verification.md")

    def test_minimal_verification_tables_are_rejected_without_date_or_reference(self):
        tables = (
            "| UC・系列 ID | 合否 |\n| --- | --- |\n| UC-1-M | 合格 |",
            "| テスト | 結果 |\n| --- | --- |\n| E2E | 合格 |",
            "| **UC・系列 ID** | **合否** |\n| --- | --- |\n| UC-1-M | 合格 |",
            "| `Test` | `status` |\n| --- | --- |\n| Login | pass |",
        )
        for table in tables:
            with self.subTest(table=table):
                self.assert_checker_ng(
                    lambda root, table=table: self.write_extra(
                        root, "docs/old-verification.md", table
                    ),
                    "旧検証表",
                    "docs/old-verification.md:1",
                )

    def test_condition_and_expected_result_spec_table_is_allowed(self):
        def change(root):
            self.write_extra(
                root,
                "docs/specification.md",
                "| 条件 | 期待結果 |\n| --- | --- |\n| 入力が空 | エラーを表示 |",
            )

        self.assert_checker_ok(change)

    def test_expected_status_and_judgment_spec_tables_are_allowed(self):
        def change(root):
            self.write_extra(
                root,
                "docs/specification.md",
                "| 構成 | 期待するHTTPステータス |\n| --- | --- |\n| 未認証 | 401 |\n\n"
                "| テスト | 判定方法 |\n| --- | --- |\n| 空の入力 | エラーを表示する |\n\n"
                "| Test case | Expected status |\n| --- | --- |\n| Empty input | 400 |",
            )

        self.assert_checker_ok(change)

    def test_escaped_pipe_is_not_a_table_cell_separator(self):
        def change(root):
            self.write_extra(
                root,
                "docs/specification.md",
                "| テスト\\|結果 | テスト |\n| --- | --- |\n| A | B |",
            )

        self.assert_checker_ok(change)

    def test_pipe_omitted_verification_and_adr_tables_are_rejected(self):
        cases = (
            (
                "verification",
                "構成 | 実行日 | 合否 | 対象コミットまたは CI 参照\n"
                "--- | --- | --- | ---\n"
                "本番 | 2026-09-21 | 合格 | abcdef0",
                "旧検証表",
            ),
            (
                "adr",
                "ADR ID | Decision | Reason\n--- | --- | ---\n"
                "ADR-1 | Adopt | Current architecture choice",
                "ADR-1",
            ),
        )
        for name, table, needle in cases:
            with self.subTest(name=name):
                self.assert_checker_ng(
                    lambda root, table=table: self.write_extra(
                        root, "docs/legacy.md", table
                    ),
                    needle,
                )

    def test_words_in_prose_and_prohibition_are_not_enough(self):
        def change(root):
            self.append(
                root,
                "README.md",
                "この説明では、設計判断や決定履歴、合意記録、検証結果を保存しない規則を説明する。"
                "現在の仕様は正本に集約し、必要な手順だけを参照する。",
            )

        self.assert_checker_ok(change)

    def test_feature_headings_with_progress_or_verification_words_are_allowed(self):
        def change(root):
            self.append(
                root,
                "README.md",
                "## 進捗通知\n\n## 進捗表示\n\n## 検証結果を表示する画面\n\n"
                "## 開発進捗機能の仕様",
            )

        self.assert_checker_ok(change)

    def test_progress_record_headings_are_rejected(self):
        def change(root):
            self.append(
                root,
                "README.md",
                "## 進捗\n\n## 開発進捗\n\n## 作業進捗\n\n## 進捗一覧",
            )

        self.assert_checker_ng(change, "旧記録の見出し")

    def test_standards_and_agents_can_contain_old_terms(self):
        def change(root):
            self.append(root, "AGENTS.md", "## 合意記録\n- 承認原文: 標準例")
            self.append(root, "docs/standards/design-and-documentation.md", "## 合意記録\n- 提示コミット: 標準例")

        # 標準のハッシュは変更されるため、内容判定が発火しないことを出力で確認する。
        returncode, output = self.run_checker(change)
        self.assertEqual(1, returncode, output)
        self.assertIn("標準のハッシュ", output)
        self.assertNotIn("[NG] 禁止記録・重複本文:", output)

    def test_each_legacy_record_is_rejected_independently(self):
        fragments = (
            "## 完成系監査中のデザイン改善",
            "## 段階3のA案採用と実装範囲",
            "- 段階3の表示調整: 見出しと合計値を統合",
            "## 保存後通知の改訂合意",
            "- 保存後通知の完成系監査記録: 承認",
            "- 再接続の改訂合意記録: 承認",
            "- 応答の原文: 合意",
            "- 利用者の応答原文: 合意",
            "- **検証状態:** 21件合格",
            "## 進捗一覧",
            "| UC・系列 ID | 段階 | 構成 | 実行日 | コマンド | 合否 | 対象コミットまたは CI 参照 |\n"
            "| --- | --- | --- | --- | --- | --- | --- |\n"
            "| UC-1-M | 6 | 本番 | 2026-09-21 | verify | 合格 | abcdef0 |",
        )
        for fragment in fragments:
            with self.subTest(fragment=fragment):
                self.assert_checker_ng(
                    lambda root: self.append(root, "docs/architecture.md", fragment),
                    "[NG] 禁止記録・重複本文:",
                )

    def test_adjacent_duplicate_list_items_are_rejected(self):
        body = "同じ長い仕様を箇条書きの別項目に転記しても重複が分かるよう、項目の単位で比較します。現在の仕様は正本に集約し、他の場所には参照だけを置きます。"

        def change(root):
            self.write_extra(root, "docs/duplicate-list.md", "- " + body + "\n- " + body)

        self.assert_checker_ng(change, "docs/duplicate-list.md:1", "docs/duplicate-list.md:2")

    def test_duplicate_paragraphs_in_one_document_are_rejected_with_both_positions(self):
        body = "これは同一文書に二度保存された長い説明本文であり、現在の仕様を正本へ集約するための重複検査用テキストです。重複を検出する目的で十分な長さを持たせています。"

        def change(root):
            self.write_extra(root, "docs/duplicate.md", body + "\n\n" + body)

        output = self.assert_checker_ng(change, "完全一致する長い本文が重複", "docs/duplicate.md:1", "docs/duplicate.md:3")
        self.assertIn("docs/duplicate.md:1、docs/duplicate.md:3", output)

    def test_duplicate_paragraphs_across_documents_are_rejected(self):
        body = "複数文書へ同じ長い説明を転記すると正本が分散するため、この本文は重複検出の対象として配置されています。重複を検出する目的で十分な長さを持たせています。"

        def change(root):
            self.write_extra(root, "docs/one.md", body)
            self.write_extra(root, "docs/two.md", body)

        self.assert_checker_ng(change, "docs/one.md:1", "docs/two.md:1")

    def test_duplicate_table_body_rows_are_rejected_but_header_is_ignored(self):
        row = "同一の表本文を二度保存した場合に正本の所在が不明になるため、この十分に長い説明行を検出します。重複を検出する目的で十分な長さを持たせています。"

        def change(root):
            self.write_extra(
                root,
                "docs/duplicate-table.md",
                "| 項目 | 説明 |\n| --- | --- |\n| A | %s |\n| A | %s |" % (row, row),
            )

        self.assert_checker_ng(change, "docs/duplicate-table.md:3", "docs/duplicate-table.md:4")

    def test_pipe_omitted_duplicate_table_body_rows_are_rejected(self):
        row = "先頭と末尾のパイプを省略した表でも同じ長い本文行を検出できるようにするための重複検査用テキストです。十分な長さを持たせています。"

        def change(root):
            self.write_extra(
                root,
                "docs/duplicate-table.md",
                "項目 | 説明\n--- | ---\nA | %s\nA | %s" % (row, row),
            )

        self.assert_checker_ng(change, "docs/duplicate-table.md:3", "docs/duplicate-table.md:4")

    def test_code_fence_is_excluded_from_duplicate_body_check(self):
        body = "コード例として保存する長い本文は重複検査の対象外であり、実装の例示だけを目的にしています。重複を検出する目的で十分な長さを持たせています。"

        cases = (
            "```text\n%s\n%s\n```" % (body, body),
            "````markdown\n%s\n```\n\n%s\n\n%s\n````" % (body, body, body),
            "````markdown\n%s\n~~~\n\n%s\n\n%s\n````" % (body, body, body),
            "````markdown\n%s\n````python\n\n%s\n\n%s\n````" % (body, body, body),
        )
        for content in cases:
            with self.subTest(content=content):
                self.assert_checker_ok(
                    lambda root, content=content: self.write_extra(
                        root, "docs/examples.md", content
                    )
                )

    def test_duplicate_after_a_normally_closed_fence_is_rejected(self):
        body = "フェンスを正常に閉じた後の本文はコード例ではないため、同じ長い説明を二度置いた場合に重複として検出される必要があります。"

        for closing in ("````", "`````"):
            with self.subTest(closing=closing):
                def change(root, closing=closing):
                    self.write_extra(
                        root,
                        "docs/examples.md",
                        "````markdown\nコード例\n%s\n\n%s\n\n%s" % (closing, body, body),
                    )

                self.assert_checker_ng(change, "docs/examples.md:5", "docs/examples.md:7")

    def test_short_boilerplate_and_link_only_guidance_are_ignored(self):
        link = "[現在の仕様を参照してください。必要な説明をまとめた正本へのリンクです。](project.md)"

        def change(root):
            self.write_extra(root, "docs/guidance-one.md", "了解しました。\n\n" + link)
            self.write_extra(root, "docs/guidance-two.md", "了解しました。\n\n" + link)

        self.assert_checker_ok(change)

    def test_similar_but_different_paragraphs_are_not_duplicates(self):
        def change(root):
            self.write_extra(
                root,
                "docs/similar.md",
                "この長い説明は正本を一箇所に集約し、参照先だけを更新するための検査用本文です。"
                "重複検出の境界を確認するため十分な長さを持たせています。"
                "\n\nこの長い説明は正本を複数箇所に分散し、参照先だけを更新するための検査用本文です。"
                "重複検出の境界を確認するため十分な長さを持たせています。",
            )

        self.assert_checker_ok(change)

    def test_pipe_in_prose_is_not_a_table(self):
        body = "表ではない本文にパイプ記号を含めても、ヘッダーと区切り行がなければ通常の文章として扱うための十分に長い検査用テキストです。"

        def change(root):
            self.write_extra(
                root,
                "docs/prose.md",
                "%s | 列A\n%s | 列A\n%s | 列A" % (body, body, body),
            )

        self.assert_checker_ok(change)


if __name__ == "__main__":
    unittest.main()
