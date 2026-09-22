"""Generate every extension and verify the distribution boundary."""
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path


SOURCE = Path(__file__).resolve().parents[1]
KINDS = ("wails", "react", "react-dotnet")
DOTNET_GENERATED_NAMES = {".vs", "bin", "obj", "TestResults", "node_modules", "dist", "data",
                          "release", "coverage", ".e2e-results", "playwright-report", ".env", "mise.local.props"}


class InitTemplateTests(unittest.TestCase):
    def generate(self, kind, destination):
        return subprocess.run(
            ["node", str(SOURCE / "scripts/init-template.mjs"), kind, str(destination)],
            capture_output=True, encoding="utf-8",
        )

    def test_distribution_contents_and_existing_destination(self):
        for kind in KINDS:
            with self.subTest(kind=kind), tempfile.TemporaryDirectory(prefix="aidd-init-") as parent:
                destination = Path(parent) / "日本語 project"
                result = self.generate(kind, destination)
                self.assertEqual(result.returncode, 0, result.stderr)
                expected = {}
                for source in (SOURCE / "template", SOURCE / f"{kind}-template"):
                    for path in source.rglob("*"):
                        name = path.relative_to(source)
                        if source.name == "react-dotnet-template" and (
                            DOTNET_GENERATED_NAMES.intersection(name.parts)
                            or name.as_posix() == "frontend/src/routeTree.gen.ts"
                        ):
                            continue
                        if path.is_file() and '.vs' not in path.relative_to(source).parts:
                            expected[path.relative_to(source)] = path.read_bytes()
                expected[Path("LICENSE")] = (SOURCE / "LICENSE").read_bytes()
                actual = {p.relative_to(destination): p.read_bytes()
                          for p in destination.rglob("*") if p.is_file()}
                self.assertEqual(actual.keys(), expected.keys())
                self.assertFalse((destination / ".vs").exists())
                if kind == "react-dotnet":
                    for name in ("backend/bin", "backend/obj", "backend/data", "backend/mise.local.props", "data", ".env"):
                        self.assertFalse((destination / name).exists(), name)
                for name, content in expected.items():
                    self.assertEqual(actual[name], content, str(name))
                for name in ("AGENTS.md", "scripts/doc_check.py",
                             "docs/standards/design-and-documentation.md",
                             "docs/standards/mock-driven-development.md",
                             ".agents/skills/usecase-docs/SKILL.md",
                             ".agents/skills/usecase-docs/assets/usecase.md",
                             ".agents/skills/usecase-docs/assets/scenario.md"):
                    self.assertFalse((SOURCE / f"{kind}-template" / name).exists())
                    self.assertEqual((destination / name).read_bytes(),
                                     (SOURCE / "template" / name).read_bytes())
                checked = subprocess.run(
                    [sys.executable, "scripts/doc_check.py", "."], cwd=destination,
                    capture_output=True, encoding="utf-8",
                )
                self.assertEqual(checked.returncode, 0, checked.stdout + checked.stderr)
                rejected = self.generate(kind, destination)
                self.assertNotEqual(rejected.returncode, 0)
                self.assertIn("既に存在", rejected.stderr)
                after = {p.relative_to(destination): p.read_bytes()
                         for p in destination.rglob("*") if p.is_file()}
                self.assertEqual(actual, after)

    def test_sources_cannot_be_output_destinations(self):
        for source in ("template", *(f"{kind}-template" for kind in KINDS)):
            for kind in KINDS:
                with self.subTest(source=source, kind=kind):
                    destination = SOURCE / source / "must-not-create"
                    self.assertFalse(destination.exists())
                    rejected = self.generate(kind, destination)
                    self.assertNotEqual(rejected.returncode, 0)
                    self.assertIn("外に指定", rejected.stderr)
                    self.assertFalse(destination.exists())


if __name__ == "__main__":
    unittest.main()
