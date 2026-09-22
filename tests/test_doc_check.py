"""doc_check.py の文書構造・参照・禁止記録判定の回帰テスト。"""

from __future__ import annotations

import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from urllib.parse import quote


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
    def write_design_docs(cls, root, files):
        for name, text in files.items():
            cls.write_utf8(root / "docs" / "design" / name, text)

    @classmethod
    def write_extra(cls, root, relative, text):
        cls.write_utf8(root / relative, text.rstrip() + "\n")

    def test_template_is_clean(self):
        self.assert_checker_ok()

    @classmethod
    def add_scenario(cls, parent, name, kind="拡張", listed=True):
        condition = "開始条件" if kind == "主成功" else "分岐条件"
        path = parent.parent / "scenarios" / (name + ".md")
        cls.write_utf8(path, "# %s\n\n- 種別: %s\n- UI確認: 要\n\n"
                       "## %s\n\n利用者が保存を指示する。\n\n"
                       "## 手順\n\n1. %s。\n\n"
                       "## 受け入れ条件\n\n処理結果を画面に表示する。\n"
                       % (name, kind, condition, name))
        if listed:
            text = parent.read_text(encoding="utf-8")
            cls.write_utf8(parent, text.replace("## シナリオ\n", "## シナリオ\n\n"
                           "- [%s](scenarios/%s.md)\n" % (name, quote(name))))
        return path

    @classmethod
    def add_usecase(cls, root, name="メモを保存する"):
        parent = root / "docs" / "usecases" / name / "README.md"
        cls.write_utf8(parent, "# %s\n\n## 主アクター\n\n利用者\n\n"
                       "## 目的\n\n%s。\n\n## シナリオ\n\n"
                       "## 実現パターン\n\n[UCP-1](../../design/UCP-1.md)\n" % (name, name))
        cls.add_scenario(parent, "新規メモを保存する", "主成功")
        project = root / "docs" / "project.md"
        lines = project.read_text(encoding="utf-8").splitlines()
        header = next(index for index, line in enumerate(lines)
                      if line.startswith("| ユースケース |"))
        lines.insert(header + 2, "| [%s](usecases/%s/README.md) | 利用者 | %s | 1 |"
                     " [UCP-1](design/UCP-1.md) | 対象 |" % (name, quote(name), name))
        cls.write_utf8(project, "\n".join(lines) + "\n")
        return parent

    def test_usecase_creation_and_extension_addition(self):
        def change(root):
            parent = self.add_usecase(root)
            self.add_scenario(parent, "重複するタイトルを拒否する")

        output = self.assert_checker_ok(change)
        self.assertIn("ユースケース 1 件、シナリオ 2 件", output)
        self.assertIn("メモを保存する シナリオ 2 本", output)

    def test_names_with_spaces_and_renamed_scenario_links(self):
        def change(root):
            parent = self.add_usecase(root, "作業 メモを保存する")
            original = parent.parent / "scenarios" / "新規メモを保存する.md"
            renamed = original.with_name("新規 メモを保存する.md")
            original.rename(renamed)
            self.write_utf8(renamed, renamed.read_text(encoding="utf-8")
                            .replace("新規メモを保存する", "新規 メモを保存する"))
            self.write_utf8(parent, parent.read_text(encoding="utf-8")
                            .replace(quote("新規メモを保存する"), quote("新規 メモを保存する"))
                            .replace("[新規メモを保存する]", "[新規 メモを保存する]"))

        self.assert_checker_ok(change)

    def test_renamed_scenario_requires_updated_parent_link(self):
        def change(root):
            parent = self.add_usecase(root)
            original = parent.parent / "scenarios" / "新規メモを保存する.md"
            renamed = original.with_name("名前を変更したシナリオ.md")
            original.rename(renamed)
            self.write_utf8(renamed, renamed.read_text(encoding="utf-8")
                            .replace("新規メモを保存する", renamed.stem))

        self.assert_checker_ng(change, "リンク先が存在しない", "親のシナリオ一覧に参照がない")

    def test_parent_and_scenario_names_must_match_their_paths(self):
        for target in ("README.md", "scenarios/新規メモを保存する.md"):
            with self.subTest(target=target):
                def change(root, target=target):
                    parent = self.add_usecase(root)
                    path = parent.parent / target
                    text = path.read_text(encoding="utf-8")
                    self.write_utf8(path, "# 異なる名前\n" + text.split("\n", 1)[1])

                self.assert_checker_ng(change, "先頭見出しは配置名と一致")

    def test_required_sections_are_not_optional_or_empty(self):
        cases = (
            ("README.md", "主アクター"), ("README.md", "目的"),
            ("README.md", "実現パターン"),
            ("scenarios/新規メモを保存する.md", "開始条件"),
            ("scenarios/新規メモを保存する.md", "手順"),
            ("scenarios/新規メモを保存する.md", "受け入れ条件"),
        )
        for target, title in cases:
            with self.subTest(target=target, title=title):
                def change(root, target=target, title=title):
                    parent = self.add_usecase(root)
                    path = parent.parent / target
                    text = path.read_text(encoding="utf-8")
                    self.write_utf8(path, text.replace("## " + title, "## 記入漏れ"))

                self.assert_checker_ng(change, "`## %s` は空でない節が1件必要" % title)

    def test_empty_duplicate_and_placeholder_sections_are_rejected(self):
        cases = (
            ("## 目的\n\nメモを保存する。", "## 目的\n\n<!-- 未記入 -->", "空でない節"),
            ("## 目的", "## 目的\n\n目的を記載する。\n\n## 目的", "空でない節"),
            ("メモを保存する。", "{{GOAL}}", "未記入の {{...}}"),
        )
        for old, new, needle in cases:
            with self.subTest(new=new):
                def change(root, old=old, new=new):
                    parent = self.add_usecase(root)
                    self.write_utf8(parent, parent.read_text(encoding="utf-8").replace(old, new))

                self.assert_checker_ng(change, needle)

    def test_scenario_choice_fields_are_required_and_constrained(self):
        for old, new in (("- 種別: 主成功", "- 種別: 正常"),
                         ("- UI確認: 要", "- UI確認: 任意"),
                         ("- UI確認: 要", ""),
                         ("- UI確認: 要", "- UI確認: 要\n- UI確認: 不要")):
            with self.subTest(new=new):
                def change(root, old=old, new=new):
                    parent = self.add_usecase(root)
                    path = parent.parent / "scenarios" / "新規メモを保存する.md"
                    self.write_utf8(path, path.read_text(encoding="utf-8").replace(old, new))

                self.assert_checker_ng(change, "いずれかを1件記載する")

    def test_extension_requires_its_branch_condition(self):
        def change(root):
            parent = self.add_usecase(root)
            path = self.add_scenario(parent, "タイトル重複を拒否する")
            self.write_utf8(path, path.read_text(encoding="utf-8").replace("## 分岐条件", "## 開始条件"))

        self.assert_checker_ng(change, "`## 分岐条件` は空でない節が1件必要")

    def test_parent_links_require_complete_unique_own_scenarios(self):
        cases = ("unlisted", "duplicate", "other-parent", "prose")
        for case in cases:
            with self.subTest(case=case):
                def change(root, case=case):
                    parent = self.add_usecase(root)
                    if case == "unlisted":
                        self.add_scenario(parent, "未掲載シナリオ", listed=False)
                    elif case == "duplicate":
                        self.add_scenario(parent, "新規メモを保存する", "主成功")
                    elif case == "other-parent":
                        other = self.add_usecase(root, "メモを削除する")
                        self.write_utf8(parent, parent.read_text(encoding="utf-8").replace(
                            "## シナリオ\n", "## シナリオ\n\n- [別のユースケースのシナリオ]"
                            "(../%s/scenarios/%s.md)\n" % (quote(other.parent.name), quote("新規メモを保存する"))))
                    else:
                        self.write_utf8(parent, parent.read_text(encoding="utf-8")
                                        .replace("## シナリオ\n", "## シナリオ\n\n本文の転記。\n"))

                needles = {"unlisted": "親のシナリオ一覧に参照がない", "duplicate": "重複した参照",
                           "other-parent": "同じユースケース", "prose": "個別相対 Markdown リンクだけ"}
                self.assert_checker_ng(change, needles[case])

    def test_implementation_pattern_requires_a_design_link(self):
        def change(root):
            parent = self.add_usecase(root)
            self.write_utf8(parent, parent.read_text(encoding="utf-8")
                            .replace("[UCP-1](../../design/UCP-1.md)", "UCP-1"))

        self.assert_checker_ng(change, "実現パターンには設計へのリンクが必要")

    def test_exactly_one_main_success_scenario_is_required(self):
        for count in (0, 2):
            with self.subTest(count=count):
                def change(root, count=count):
                    parent = self.add_usecase(root)
                    if count == 0:
                        self.add_scenario(parent, "新規メモを保存する", "拡張", listed=False)
                    else:
                        self.add_scenario(parent, "別の主成功", "主成功")

                self.assert_checker_ng(change, "主成功シナリオはちょうど1件必要")

    def test_scenario_without_parent_and_legacy_placement_are_rejected(self):
        def orphan(root):
            self.add_scenario(root / "docs" / "usecases" / "親のない仕様" / "README.md",
                              "孤立したシナリオ", listed=False)

        self.assert_checker_ng(orphan, "所属するユースケースの README.md がない")
        for relative in ("docs/usecases/UC-99.md", "docs/usecases/仕様/extra.md",
                         "docs/usecases/仕様/scenarios/nested/深すぎる.md"):
            with self.subTest(relative=relative):
                self.assert_checker_ng(lambda root, relative=relative:
                    self.write_extra(root, relative, "# 旧配置の仕様"), "配置は <名称>/README.md")

    def test_catalog_allows_unstarted_names_but_requires_existing_parents(self):
        output = self.assert_checker_ok()
        self.assertIn("ユースケース 0 件、シナリオ 0 件", output)

        def change(root):
            parent = self.add_usecase(root)
            project = root / "docs" / "project.md"
            text = project.read_text(encoding="utf-8")
            self.write_utf8(project, text.replace("[%s](usecases/%s/README.md)"
                            % (parent.parent.name, quote(parent.parent.name)), parent.parent.name))

        self.assert_checker_ng(change, "ユースケース一覧に参照がない")

    def test_template_assets_are_excluded_but_skill_links_are_checked(self):
        relative = ".agents/skills/usecase-docs/assets/unfinished.md"

        def asset(root):
            self.write_extra(root, relative, "# {{NAME}}\n\n[未記入](missing.md)\n\n## 合意記録")

        self.assert_checker_ok(asset)

        def skill(root):
            asset(root)
            self.write_extra(root, ".agents/skills/usecase-docs/SKILL.md", "# スキル\n\n[不足](missing.md)")

        self.assert_checker_ng(skill, "SKILL.md", "リンク先が存在しない")

    def test_encoded_absolute_paths_are_detected(self):
        self.assert_checker_ng(lambda root: self.write_extra(root, "docs/path.md",
                               "[端末内の文書](C%3A%2FUsers%2Fexample%2Fmemo.md)"), "ローカル絶対パス")

    def test_old_identifiers_are_allowed_in_explanatory_prose(self):
        def change(root):
            parent = self.add_usecase(root)
            self.append(root, str(parent.relative_to(root)), "この文書は旧 UC-1 の内容に対応する。")

        self.assert_checker_ok(change)

    def test_reports_all_ucp_documents_but_not_other_design_documents(self):
        def change(root):
            self.write_design_docs(root, {
                "UCP-1.md": "# UCP-1. 最初のパターン\n\n# UCP-88. 本文中の見出し\n",
                "UCP-2.md": "# UCP-2. 次のパターン\n",
            })
            data_path = root / "docs" / "design" / "data.md"
            self.write_utf8(
                data_path,
                "# UCP-99. 対象外ファイルの見出し\n\n"
                + data_path.read_text(encoding="utf-8"),
            )
            architecture_path = root / "docs" / "architecture.md"
            self.write_utf8(
                architecture_path,
                architecture_path.read_text(encoding="utf-8")
                + "\n### UCP-99. 旧形式のパターン\n",
            )

        output = self.assert_checker_ok(change)
        self.assertIn("docs/design/ の UCP 文書に 2 件", output)
        self.assertNotIn("architecture.md に", output)

    def test_broken_link_in_design_document_is_detected(self):
        def change(root):
            self.write_design_docs(
                root,
                {"UCP-1.md": "# UCP-1. パターン\n\n[存在しない設計](missing.md)\n"},
            )

        self.assert_checker_ng(change, "UCP-1.md", "リンク先が存在しない")

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
