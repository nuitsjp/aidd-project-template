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
PRODUCT_FILES = (
    ".env.example", ".nvmrc", ".prettierignore", ".prettierrc.json", "App.slnx", "global.json", "package.json", "package-lock.json",
    "eslint.config.mjs", "playwright.config.ts", "vitest.config.ts", "tsconfig.json",
    "tsr.config.json", "mise.toml", "backend", "frontend", "contracts", "tests", "scripts",
)
PRODUCT_NAME = "Acme.Notes"
PRODUCT_RENAMES = (
    (b"Backend.IntegrationTests", b"Acme.Notes.IntegrationTests"),
    (b"Backend.UnitTests", b"Acme.Notes.UnitTests"),
    (b"Frontend.esproj", b"Acme.Notes.Frontend.esproj"),
    (b"App.slnx", b"Acme.Notes.slnx"),
    (b"App.csproj", b"Acme.Notes.csproj"),
    (b"App.dll", b"Acme.Notes.dll"),
    (b"App.Migrations.", b"Acme.Notes.Migrations."),
    (b"<AssemblyName>App</AssemblyName>", b"<AssemblyName>Acme.Notes</AssemblyName>"),
    ("使用方法: App db:".encode(), "使用方法: Acme.Notes db:".encode()),
)
PRODUCT_PATHS = {
    "App.slnx": "Acme.Notes.slnx",
    "backend/App.csproj": "backend/Acme.Notes.csproj",
    "tests/dotnet-unit/Backend.UnitTests.csproj": "tests/dotnet-unit/Acme.Notes.UnitTests.csproj",
    "tests/dotnet-integration/Backend.IntegrationTests.csproj": "tests/dotnet-integration/Acme.Notes.IntegrationTests.csproj",
    "frontend/Frontend.esproj": "frontend/Acme.Notes.Frontend.esproj",
}


class InitTemplateTests(unittest.TestCase):
    def generate(self, kind, destination, name=PRODUCT_NAME):
        args = ["node", str(SOURCE / "scripts/init-template.mjs"), kind, str(destination)]
        if kind == "react-dotnet":
            args.extend(("--name", name))
        return subprocess.run(
            args,
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
                            or name.as_posix() == "reference/frontend/src/routeTree.gen.ts"
                        ):
                            continue
                        if path.is_file() and '.vs' not in path.relative_to(source).parts:
                            expected[path.relative_to(source)] = path.read_bytes()
                expected[Path("LICENSE")] = (SOURCE / "LICENSE").read_bytes()
                if kind == "react-dotnet":
                    reference = SOURCE / "react-dotnet-template/reference"
                    for item in PRODUCT_FILES:
                        source = reference / item
                        paths = source.rglob("*") if source.is_dir() else (source,)
                        for path in paths:
                            source_name = path.relative_to(reference)
                            if DOTNET_GENERATED_NAMES.intersection(source_name.parts) or source_name.as_posix() in (
                                "frontend/src/routeTree.gen.ts", "scripts/check-docs.mjs",
                            ) or not path.is_file():
                                continue
                            content = path.read_bytes()
                            if path.suffix in (".cs", ".csproj"):
                                content = content.replace(b"NotesSample", PRODUCT_NAME.encode())
                            for before, after in PRODUCT_RENAMES:
                                content = content.replace(before, after)
                            if source_name.as_posix() == "backend/Properties/launchSettings.json":
                                content = content.replace(b'"App": {', b'"Acme.Notes": {')
                            if source_name.as_posix() in ("tests/dotnet-unit/packages.lock.json", "tests/dotnet-integration/packages.lock.json"):
                                content = content.replace(b'"app": {', b'"acme.notes": {')
                            if source_name.as_posix() == "contracts/openapi.json":
                                content = content.replace(b'"App | v1"', b'"Acme.Notes | v1"')
                                content = content.replace(b'"App"', b'"Acme.Notes"')
                            if source_name.as_posix() in ("package.json", "package-lock.json"):
                                content = content.replace(b"aidd-react-dotnet-template", b"acme-notes")
                            expected[Path(PRODUCT_PATHS.get(source_name.as_posix(), source_name.as_posix()))] = content
                else:
                    reference = SOURCE / f"{kind}-template/reference"
                    for path in reference.rglob("*"):
                        source_name = path.relative_to(reference)
                        if path.is_file() and source_name.parts[0] not in ("README.md", "docs"):
                            expected[source_name] = path.read_bytes()
                actual = {p.relative_to(destination): p.read_bytes()
                          for p in destination.rglob("*") if p.is_file()}
                self.assertEqual(actual.keys(), expected.keys())
                self.assertFalse((destination / ".vs").exists())
                if kind == "react-dotnet":
                    for name in ("reference/backend/bin", "reference/backend/obj", "reference/backend/data", "reference/backend/mise.local.props", "reference/data", "reference/.env"):
                        self.assertFalse((destination / name).exists(), name)
                    self.assertTrue((destination / "reference/backend/App.csproj").is_file())
                    self.assertTrue((destination / "reference/App.slnx").is_file())
                    self.assertTrue((destination / "backend/Acme.Notes.csproj").is_file())
                    self.assertTrue((destination / "Acme.Notes.slnx").is_file())
                    self.assertTrue((destination / "tests/dotnet-unit/Acme.Notes.UnitTests.csproj").is_file())
                    self.assertTrue((destination / "tests/dotnet-integration/Acme.Notes.IntegrationTests.csproj").is_file())
                    self.assertTrue((destination / "frontend/Acme.Notes.Frontend.esproj").is_file())
                    self.assertFalse((destination / "backend/App.csproj").exists())
                    self.assertFalse((destination / "App.slnx").exists())
                    self.assertIn(b"NotesSample", (destination / "reference/backend/App.csproj").read_bytes())
                    self.assertIn(b"Acme.Notes", (destination / "backend/Acme.Notes.csproj").read_bytes())
                    self.assertNotIn(b"NotesSample", (destination / "backend/Acme.Notes.csproj").read_bytes())
                    root_tasks = (destination / "mise.toml").read_text(encoding="utf-8")
                    reference_tasks = (destination / "reference/mise.toml").read_text(encoding="utf-8")
                    self.assertIn('[tasks."check:docs"]', root_tasks)
                    self.assertIn("[tasks.verify]", root_tasks)
                    self.assertIn("[tasks.verify]", reference_tasks)
                    self.assertFalse((destination / "scripts/reference-task.mjs").exists())
                self.assertEqual(list((destination / "docs/usecases").glob("*/README.md")), [])
                self.assertEqual(len(list((destination / "reference/docs/usecases").glob("*/README.md"))),
                                 3 if kind == "wails" else 2)
                for name, content in expected.items():
                    self.assertEqual(actual[name], content, str(name))
                if kind == "react-dotnet":
                    second = Path(parent) / "another location"
                    regenerated = self.generate(kind, second)
                    self.assertEqual(regenerated.returncode, 0, regenerated.stderr)
                    self.assertEqual(actual, {p.relative_to(second): p.read_bytes()
                                              for p in second.rglob("*") if p.is_file()})
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
                reference_checked = subprocess.run(
                    [sys.executable, "scripts/doc_check.py", "reference"], cwd=destination,
                    capture_output=True, encoding="utf-8",
                )
                self.assertEqual(reference_checked.returncode, 0,
                                 reference_checked.stdout + reference_checked.stderr)
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

    def test_dotnet_requires_valid_project_name(self):
        with tempfile.TemporaryDirectory(prefix="aidd-init-") as parent:
            for name in ("", "class", "Wrong Name", "Company..Product"):
                with self.subTest(name=name):
                    destination = Path(parent) / "invalid"
                    result = self.generate("react-dotnet", destination, name)
                    self.assertNotEqual(result.returncode, 0)
                    self.assertFalse(destination.exists())


if __name__ == "__main__":
    unittest.main()
