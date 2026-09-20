"""doc_check.py の段階・合意・監査の回帰テスト。"""

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

COMMIT = "abcdef0123456789abcdef0123456789abcdef01"
AUDIT_COMMIT = "1234567890abcdef1234567890abcdef12345678"


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
        path.write_bytes(text.encode("utf-8"))

    @classmethod
    def set_verification_rows(cls, root, rows):
        """第6節の表を指定された行へ置換する。"""
        path = root / "docs" / "project.md"
        lines = path.read_text(encoding="utf-8").splitlines()
        section = next(i for i, line in enumerate(lines) if line.startswith("## 6."))
        table = next(i for i in range(section + 1, len(lines)) if lines[i].lstrip().startswith("|"))
        end = table
        while end < len(lines) and lines[end].lstrip().startswith("|"):
            end += 1
        replacement = [
            "| UC・系列 ID | 段階 | 構成 | 実行日 | コマンド | 合否 | 対象コミットまたは CI 参照 |",
            "| --- | --- | --- | --- | --- | --- | --- |",
        ]
        replacement.extend(
            "| %s | %s | %s | %s | %s | %s | %s |" % row for row in rows
        )
        cls.write_utf8(path, "\n".join(lines[:table] + replacement + lines[end:]) + "\n")

    @classmethod
    def set_verification_rows_without_stage(cls, root, rows):
        """第6節の表から段階列を除いた不正な表を作る。"""
        path = root / "docs" / "project.md"
        lines = path.read_text(encoding="utf-8").splitlines()
        section = next(i for i, line in enumerate(lines) if line.startswith("## 6."))
        table = next(i for i in range(section + 1, len(lines)) if lines[i].lstrip().startswith("|"))
        end = table
        while end < len(lines) and lines[end].lstrip().startswith("|"):
            end += 1
        replacement = [
            "| UC・系列 ID | 構成 | 実行日 | コマンド | 合否 | 対象コミットまたは CI 参照 |",
            "| --- | --- | --- | --- | --- | --- |",
        ]
        replacement.extend(
            "| %s | %s | %s | %s | %s | %s |" % row for row in rows
        )
        cls.write_utf8(path, "\n".join(lines[:table] + replacement + lines[end:]) + "\n")

    @classmethod
    def add_series(cls, root, *series):
        """UC-1 本文へ拡張系列の記述を追加する。"""
        if not series:
            return
        path = root / "docs" / "usecases" / "UC-1.md"
        text = path.read_text(encoding="utf-8").rstrip() + "\n\n"
        text += "\n".join("- 拡張系列 %s: 拡張シナリオ" % item for item in series)
        cls.write_utf8(path, text + "\n")

    @classmethod
    def set_records(cls, root, agreements=(), audits=(), agreement_quote="合意します。",
                    audit_quote="承認します。", agreement_commit=COMMIT,
                    audit_commit=AUDIT_COMMIT):
        """UC-1 の合意記録と完成系監査記録を独立して置換する。"""
        path = root / "docs" / "usecases" / "UC-1.md"
        lines = path.read_text(encoding="utf-8").splitlines()
        agreement = next(i for i, line in enumerate(lines) if line.startswith("- 合意記録"))
        audit = next(i for i, line in enumerate(lines) if line.startswith("- 完成系監査記録"))
        end = next(i for i in range(audit + 1, len(lines)) if lines[i].startswith("合意記録の4項目"))

        replacement = [lines[agreement]]
        for series in agreements:
            replacement.extend([
                "  - %s / 提示コミット: %s / 論点と回答: 既定でよい" % (series, agreement_commit),
                "    > %s" % agreement_quote,
            ])
        replacement.append(lines[audit])
        for series in audits:
            replacement.extend([
                "  - %s / 提示コミット: %s" % (series, audit_commit),
                "    > %s" % audit_quote,
            ])
        cls.write_utf8(path, "\n".join(lines[:agreement] + replacement + lines[end:]) + "\n")

    @classmethod
    def set_overall_agreement(cls, root, quote="全体設計に合意します。"):
        path = root / "docs" / "architecture.md"
        text = path.read_text(encoding="utf-8")
        text = text.replace("{{COMMIT_HASH}}", COMMIT).replace("{{USER_RESPONSE}}", quote)
        cls.write_utf8(path, text)

    @classmethod
    def complete_series(cls, root, series=("UC-1-M",), rows=None):
        if rows is None:
            rows = [("%s" % series[0], "6", "本番", "2026-09-20", "verify", "合格", COMMIT)]
        cls.set_records(root, agreements=series, audits=series)
        cls.set_overall_agreement(root)
        cls.set_verification_rows(root, rows)

    def test_unfilled_template_is_clean_and_reports_series_counts(self):
        output = self.assert_checker_ok()
        self.assertIn("記述済みの系列 1 本", output)
        self.assertIn("拡張 0 本", output)

    def test_intermediate_mock_result_does_not_require_audit(self):
        self.assert_checker_ok(
            lambda root: self.set_verification_rows(
                root, [("UC-1-M", "2", "モック", "2026-09-20", "playwright", "合格", COMMIT)]
            )
        )

    def test_two_mock_results_without_audit_remain_in_progress(self):
        def change(root):
            self.add_series(root, "UC-1-X1")
            self.set_records(root, agreements=("UC-1-M", "UC-1-X1"))
            self.set_overall_agreement(root)
            self.set_verification_rows(root, [
                ("UC-1-M", "2", "モック", "2026-09-20", "playwright", "合格", COMMIT),
                ("UC-1-X1", "3", "モック", "2026-09-20", "playwright", "合格", COMMIT),
            ])

        self.assert_checker_ng(change, "仕掛かり", "UC-1-M", "UC-1-X1")

    def test_verification_table_without_stage_column_is_invalid(self):
        self.assert_checker_ng(
            lambda root: self.set_verification_rows_without_stage(
                root, [("UC-1-M", "モック", "2026-09-20", "playwright", "合格", COMMIT)]
            ),
            "段階",
        )

    def test_stage6_pass_requires_agreement_and_audit(self):
        self.assert_checker_ng(
            lambda root: self.set_verification_rows(
                root, [("UC-1-M", "6", "本番", "2026-09-20", "verify", "合格", COMMIT)]
            ),
            "UC-1-M",
            "合意",
            "監査",
        )

    def test_stage6_pass_with_agreement_only_requires_audit(self):
        for result in ("合格", "不合格"):
            with self.subTest(result=result):
                def change(root, result=result):
                    self.set_records(root, agreements=("UC-1-M",))
                    self.set_overall_agreement(root)
                    self.set_verification_rows(
                        root, [("UC-1-M", "6", "本番", "2026-09-20", "verify", result, COMMIT)]
                    )

                self.assert_checker_ng(change, "UC-1-M", "監査")

    def test_stage6_pass_with_audit_only_cannot_replace_agreement(self):
        def change(root):
            self.set_records(root, audits=("UC-1-M",))
            self.set_overall_agreement(root)
            self.set_verification_rows(
                root, [("UC-1-M", "6", "本番", "2026-09-20", "verify", "合格", COMMIT)]
            )

        self.assert_checker_ng(change, "UC-1-M", "合意")

    def test_stage6_pass_with_both_records_is_complete(self):
        self.assert_checker_ok(self.complete_series)

    def test_placeholder_agreement_quote_is_not_a_record(self):
        def change(root):
            self.set_records(
                root,
                agreements=("UC-1-M",),
                audits=("UC-1-M",),
                agreement_quote="{{USER_RESPONSE}}",
            )
            self.set_overall_agreement(root)
            self.set_verification_rows(
                root, [("UC-1-M", "6", "本番", "2026-09-20", "verify", "合格", COMMIT)]
            )

        self.assert_checker_ng(change, "UC-1-M", "引用")

    def test_placeholder_audit_quote_is_not_a_record(self):
        def change(root):
            self.set_records(
                root,
                agreements=("UC-1-M",),
                audits=("UC-1-M",),
                audit_quote="{{AUDIT_USER_RESPONSE}}",
            )
            self.set_overall_agreement(root)
            self.set_verification_rows(
                root, [("UC-1-M", "6", "本番", "2026-09-20", "verify", "合格", COMMIT)]
            )

        self.assert_checker_ng(change, "UC-1-M", "監査")

    def test_placeholder_audit_commit_is_not_a_record(self):
        def change(root):
            self.set_records(
                root,
                agreements=("UC-1-M",),
                audits=("UC-1-M",),
                audit_commit="{{AUDIT_COMMIT_HASH}}",
            )
            self.set_overall_agreement(root)
            self.set_verification_rows(
                root, [("UC-1-M", "6", "本番", "2026-09-20", "verify", "合格", COMMIT)]
            )

        self.assert_checker_ng(change, "UC-1-M", "完成系監査記録", "提示コミットのハッシュ")

    def test_audit_quote_does_not_bleed_into_agreement_record(self):
        def change(root):
            self.set_records(root, agreements=("UC-1-M",), audits=("UC-1-M",), agreement_quote="")
            self.set_overall_agreement(root)
            self.set_verification_rows(
                root, [("UC-1-M", "6", "本番", "2026-09-20", "verify", "合格", COMMIT)]
            )

        self.assert_checker_ng(change, "UC-1-M", "合意", "引用")

    def test_stage6_pass_with_blank_stage_is_invalid(self):
        def change(root):
            self.complete_series(root, rows=[("UC-1-M", "", "本番", "2026-09-20", "verify", "合格", COMMIT)])

        self.assert_checker_ng(change, "段階")

    def test_stage6_pass_with_invalid_stage_is_invalid(self):
        def change(root):
            self.complete_series(root, rows=[("UC-1-M", "7", "本番", "2026-09-20", "verify", "合格", COMMIT)])

        self.assert_checker_ng(change, "段階")

    def test_all_stage6_configurations_for_a_series_must_pass(self):
        def change(root):
            self.add_series(root, "UC-1-X1")
            self.set_records(root, agreements=("UC-1-M", "UC-1-X1"),
                             audits=("UC-1-M", "UC-1-X1"))
            self.set_overall_agreement(root)
            self.set_verification_rows(root, [
                ("UC-1-M", "6", "本番A", "2026-09-20", "verify", "合格", COMMIT),
                ("UC-1-M", "6", "本番B", "2026-09-20", "verify", "不合格", COMMIT),
                ("UC-1-X1", "6", "本番", "2026-09-20", "verify", "未検証", "—"),
            ])

        self.assert_checker_ng(change, "UC-1-M", "UC-1-X1")

    def test_failed_and_unverified_stage6_configurations_remain_in_progress(self):
        def change(root):
            self.add_series(root, "UC-1-X1", "UC-1-X2")
            self.set_records(root, agreements=("UC-1-M", "UC-1-X1", "UC-1-X2"),
                             audits=("UC-1-M", "UC-1-X1", "UC-1-X2"))
            self.set_overall_agreement(root)
            self.set_verification_rows(root, [
                ("UC-1-M", "6", "本番", "2026-09-20", "verify", "合格", COMMIT),
                ("UC-1-X1", "6", "本番", "2026-09-20", "verify", "不合格", COMMIT),
                ("UC-1-X2", "6", "本番", "2026-09-20", "verify", "未検証", "—"),
            ])

        output = self.assert_checker_ng(change, "UC-1-X1", "UC-1-X2")
        self.assertIn("仕掛かり", output)

    def test_multiple_series_in_one_result_row_are_all_checked(self):
        def change(root):
            self.add_series(root, "UC-1-X1")
            self.set_records(root, agreements=("UC-1-M",), audits=("UC-1-M",))
            self.set_overall_agreement(root)
            self.set_verification_rows(root, [
                ("UC-1-M / UC-1-X1", "6", "本番", "2026-09-20", "verify", "合格", COMMIT),
            ])

        self.assert_checker_ng(change, "UC-1-X1")

    def test_multiple_series_in_one_result_row_can_all_complete(self):
        def change(root):
            self.add_series(root, "UC-1-X1")
            self.set_records(root, agreements=("UC-1-M", "UC-1-X1"), audits=("UC-1-M", "UC-1-X1"))
            self.set_overall_agreement(root)
            self.set_verification_rows(root, [
                ("UC-1-M / UC-1-X1", "6", "本番", "2026-09-20", "verify", "合格", COMMIT),
            ])

        self.assert_checker_ok(change)


if __name__ == "__main__":
    unittest.main()
