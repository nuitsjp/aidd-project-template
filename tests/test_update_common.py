"""The common update command must preserve project content and reject drift."""
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path


SOURCE = Path(__file__).resolve().parents[1]
MANAGED = (
    "AGENTS.md",
    "docs/standards/design-and-documentation.md",
    "docs/standards/mock-driven-development.md",
    "scripts/doc_check.py",
)


class CommonUpdateTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workspace = tempfile.TemporaryDirectory(prefix="aidd-update-")
        cls.repo = Path(cls.workspace.name) / "upstream"
        cls.repo.mkdir()
        (cls.repo / "scripts").mkdir()
        shutil.copyfile(SOURCE / "scripts/update-common.mjs", cls.repo / "scripts/update-common.mjs")
        cls.git("init", "--quiet")
        cls.git("config", "core.autocrlf", "false")
        for name in (*MANAGED, "docs/project.md"):
            path = cls.repo / "template" / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(f"original {name}\n".encode())
        cls.before = cls.snapshot("original")
        for name in (*MANAGED, "docs/project.md"):
            (cls.repo / "template" / name).write_bytes(f"updated {name}\n".encode())
        cls.after = cls.snapshot("updated")

    @classmethod
    def tearDownClass(cls):
        cls.workspace.cleanup()

    @classmethod
    def git(cls, *args):
        return subprocess.check_output(
            ["git", "-C", str(cls.repo), *args], encoding="utf-8", stderr=subprocess.STDOUT
        ).strip()

    @classmethod
    def snapshot(cls, message):
        cls.git("add", "template")
        cls.git("-c", "user.name=Update test", "-c", "user.email=test@example.invalid",
                "commit", "--quiet", "-m", message)
        return cls.git("rev-parse", "HEAD")

    def setUp(self):
        self.project_temp = tempfile.TemporaryDirectory(prefix="adopter-", dir=self.workspace.name)
        self.addCleanup(self.project_temp.cleanup)
        self.project = Path(self.project_temp.name)
        for name in MANAGED:
            path = self.project / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(f"original {name}\n".encode())
        (self.project / "docs/project.md").write_bytes("製品の仕様・合意・検証結果\n".encode())
        (self.project / "app.ts").write_bytes(b"product code\n")

    def contents(self):
        return {p.relative_to(self.project).as_posix(): p.read_bytes()
                for p in self.project.rglob("*") if p.is_file()}

    def update(self, before=None, after=None, destination=None):
        return subprocess.run(
            ["node", str(self.repo / "scripts/update-common.mjs"),
             str(destination or self.project), before or self.before, after or self.after],
            encoding="utf-8", capture_output=True,
        )

    def test_only_common_files_are_updated(self):
        existing = self.contents()
        result = self.update()
        self.assertEqual(result.returncode, 0, result.stderr)
        for name in MANAGED:
            self.assertEqual((self.project / name).read_bytes(), f"updated {name}\n".encode())
        for name in ("docs/project.md", "app.ts"):
            self.assertEqual((self.project / name).read_bytes(), existing[name])
        self.assertIn(self.after, result.stdout)

    def test_local_change_rejects_entire_update(self):
        (self.project / MANAGED[-1]).write_bytes(b"locally customized checker\n")
        existing = self.contents()
        result = self.update()
        self.assertNotEqual(result.returncode, 0)
        self.assertIn(MANAGED[-1], result.stderr)
        self.assertEqual(self.contents(), existing)

    def test_missing_file_rejects_entire_update(self):
        (self.project / MANAGED[-1]).unlink()
        existing = self.contents()
        self.assertNotEqual(self.update().returncode, 0)
        self.assertEqual(self.contents(), existing)

    def test_hard_link_to_project_content_rejects_entire_update(self):
        (self.project / "local-rules.md").hardlink_to(self.project / MANAGED[0])
        existing = self.contents()
        self.assertNotEqual(self.update().returncode, 0)
        self.assertEqual(self.contents(), existing)

    def test_non_commit_or_unknown_reference_rejects_entire_update(self):
        existing = self.contents()
        for reference in ("HEAD", self.after[:7], "f" * 40):
            with self.subTest(reference=reference):
                self.assertNotEqual(self.update(after=reference).returncode, 0)
                self.assertEqual(self.contents(), existing)

    def test_crlf_original_is_accepted(self):
        for name in MANAGED:
            path = self.project / name
            path.write_bytes(path.read_bytes().replace(b"\n", b"\r\n"))
        result = self.update()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual((self.project / MANAGED[0]).read_bytes(), f"updated {MANAGED[0]}\n".encode())

    def test_upstream_checkout_cannot_be_updated(self):
        existing = {name: (self.repo / "template" / name).read_bytes() for name in MANAGED}
        result = self.update(destination=self.repo / "template")
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual({name: (self.repo / "template" / name).read_bytes() for name in MANAGED}, existing)


if __name__ == "__main__":
    unittest.main()
