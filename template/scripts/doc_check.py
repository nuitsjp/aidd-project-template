#!/usr/bin/env python3
"""文書整合の判定スクリプト。結果は報告であり、差し戻しの判断は人が行う。"""
import argparse
import hashlib
import os
import re
import sys
from pathlib import Path

EMPTY_CELLS = {"", "-", "--", "---", "—"}
EXCLUDE_DIRS = {".git", "node_modules", "bin", "obj", "dist", "build", ".venv", "venv",
                "vendor", "upstream", "old", "poc", ".playwright-cli"}
EXCLUDE_RELPATHS = {"docs/reference"}
EVIDENCE_EXTRA_EXCLUDE = {"public", "static", "assets", "src", "frontend", "test", "tests"}
EVIDENCE_EXTS = {".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".log", ".sha256"}
# 行数上限の正本: docs/standards/design-and-documentation.md §3
LINE_LIMITS = {"docs/project.md": 300, "docs/architecture.md": 200,
               "docs/document-policy.md": 100}
PLACEHOLDER_HASH = "sha256:" + "0" * 64
# 配布元が `--print-hashes` の出力で更新する。
EXPECTED_HASHES = {
    "docs/standards/design-and-documentation.md": "sha256:6c7328e34e38d0cd10f5a03d0a3ab3ec50ce16f4d3e484bb492fc3a138b07daa",
    "docs/standards/mock-driven-development.md": "sha256:4c817c04ff41df55abda4017a5b4a8749787de75d67d9c42ed7a8d88d10c5af4",
}

HEX_RE = re.compile(r"(?<![0-9A-Za-z])[0-9a-fA-F]{7,40}(?![0-9A-Za-z])")
QUOTE_RE = re.compile(r"^\s*>\s*\S")
UC_HEAD_RE = re.compile(r"^###\s+UC-(\d+)\.")
P_HEAD_RE = re.compile(r"^###\s+UCP-(\d+)\.")
UC_ID_RE = re.compile(r"UC-\d+")
SERIES_RE = re.compile(r"UC-(\d+)-(?:M|X\d+)")
EXT_RE = re.compile(r"UC-\d+-X\d+")
LINK_RE = re.compile(r"!?\[[^\]]*\]\(([^()\s]+)(?:\s+\"[^\"]*\")?\)")
ABSPATH_RE = re.compile(r"(?<![0-9A-Za-z])[A-Za-z]:[\\/]|/Users/|/home/")
ANCHOR_ID_RE = re.compile(r"<a\s+id=\"([^\"]+)\"")
SLUG_STRIP_RE = re.compile(r"[^\w\-一-龠ぁ-んァ-ヶー]")
NG_COUNT = 0


def emit(tag, message):
    global NG_COUNT
    if tag == "NG":
        NG_COUNT += 1
    print("[%s] %s" % (tag, message))


def read_text(path):
    """BOM を除き、改行を LF に揃えて読む。"""
    return path.read_bytes().decode("utf-8-sig", errors="replace").replace("\r\n", "\n").replace("\r", "\n")


def norm_hash(path):
    body = (read_text(path).rstrip("\n") + "\n").encode("utf-8")
    return "sha256:" + hashlib.sha256(body).hexdigest()


def rel(root, path):
    try:
        return path.relative_to(root).as_posix()
    except ValueError:
        return path.name


def walk(root, extra_exclude=frozenset()):
    """除外ディレクトリを枝刈りしながら (ディレクトリ, 子ディレクトリ名, ファイル名) を返す。"""
    for dirpath, dirnames, filenames in os.walk(root):
        here = Path(dirpath)
        dirnames[:] = sorted(d for d in dirnames
                             if d not in EXCLUDE_DIRS and d not in extra_exclude
                             and rel(root, here / d) not in EXCLUDE_RELPATHS)
        yield here, list(dirnames), sorted(filenames)


def anchors_of(text):
    """`<a id="...">` と見出しの単純スラグを集めた集合を返す。"""
    ids = set(ANCHOR_ID_RE.findall(text))
    for line in text.split("\n"):
        head = line.strip()
        if head.startswith("#"):
            ids.add(SLUG_STRIP_RE.sub("", re.sub(r"\s+", "-", head.lstrip("#").strip().lower())))
    return ids


def heading_key(line):
    """見出しを (レベル, 節番号を除いた題) に正規化する。既存プロジェクトの節番号の違いを吸収する。"""
    head = line.strip()
    level = len(head) - len(head.lstrip("#"))
    return level, re.sub(r"^\d+\.\s*", "", head.lstrip("#").strip())


def section_body(text, heading):
    """見出し（節番号は無視、題は前方一致）で節本文を [(行番号, 行)] として返す。無ければ None。"""
    lines = text.split("\n")
    want_level, want_title = heading_key(heading)
    start, level = None, 0
    for i, line in enumerate(lines, 1):
        head = line.strip()
        if start is None:
            if head.startswith("#"):
                got_level, got_title = heading_key(head)
                if got_level == want_level and got_title.startswith(want_title):
                    start, level = i, got_level
        elif head.startswith("#") and len(head) - len(head.lstrip("#")) <= level:
            return list(enumerate(lines[start:i - 1], start + 1))
    return None if start is None else list(enumerate(lines[start:], start + 1))


def parse_table(body):
    """節本文の最初の表を (見出しセル, [(行番号, セル列)]) で返す。表が無ければ None。"""
    header, rows = None, []
    for lineno, line in body:
        cells = line.strip()
        if cells.startswith("|"):
            cells = [c.strip() for c in cells.strip("|").split("|")]
            if header is None:
                header = cells
            elif not all(c and set(c) <= set("-: ") for c in cells):
                rows.append((lineno, cells))
        elif header is not None and cells:
            break
    return None if header is None else (header, rows)


def cell(header, cells, name):
    if name not in header:
        return ""
    index = header.index(name)
    return cells[index] if index < len(cells) else ""


def uc_of(text):
    found = UC_ID_RE.search(text)
    return found.group(0) if found else ""


def uc_bodies(text):
    """`### UC-n.` の本文を UC ID ごとに返す（次の `###` または `##` まで）。"""
    bodies, current, buf = {}, None, []
    for line in text.split("\n"):
        matched = UC_HEAD_RE.match(line)
        if matched:
            if current:
                bodies[current] = "\n".join(buf)
            current, buf = "UC-" + matched.group(1), []
        elif current and line.startswith("##"):
            bodies[current], current, buf = "\n".join(buf), None, []
        elif current is not None:
            buf.append(line)
    if current:
        bodies[current] = "\n".join(buf)
    return bodies


def check_links(root, docs):
    """判定 1: リンク先のファイルとアンカーが解決できるか。"""
    if not docs:
        emit("対象なし", "リンク: 走査対象の Markdown がない")
        return
    anchors = {path: anchors_of(text) for path, text in docs.items()}
    bad = []
    for path, text in sorted(docs.items()):
        for lineno, line in enumerate(text.split("\n"), 1):
            for target in LINK_RE.findall(line):
                if target.startswith(("http://", "https://", "mailto:")):
                    continue
                where = "%s:%d" % (rel(root, path), lineno)
                filepart, _, anchor = target.partition("#")
                dest = path
                if filepart:
                    dest = (path.parent / filepart).resolve()
                    if not dest.exists():
                        bad.append("%s リンク先が存在しない: %s" % (where, target))
                        continue
                if anchor and dest.suffix.lower() == ".md" and dest.is_file():
                    if dest not in anchors:
                        anchors[dest] = anchors_of(read_text(dest))
                    if anchor not in anchors[dest]:
                        bad.append("%s アンカーが存在しない: %s" % (where, target))
    for message in bad:
        emit("NG", "リンク: " + message)
    if not bad:
        emit("OK", "リンク: 未解決のリンクとアンカーはない")


def check_abs_paths(root, docs):
    """判定 2: ローカル絶対パスを含む行がないか。"""
    hits = ["%s:%d" % (rel(root, path), lineno)
            for path, text in sorted(docs.items())
            for lineno, line in enumerate(text.split("\n"), 1) if ABSPATH_RE.search(line)]
    for where in hits:
        emit("NG", "ローカル絶対パス: %s にローカル絶対パスがある" % where)
    if not hits:
        emit("OK", "ローカル絶対パス: 走査対象にローカル絶対パスはない")


def check_overall_agreement(arch_text):
    """判定 3(d): 全体設計の合意欄に提示コミットと引用があるか。"""
    body = section_body(arch_text, "## 全体設計の合意") if arch_text else None
    if body is None:
        emit("NG", "仕掛かり: docs/architecture.md に `## 全体設計の合意` の節がない")
        return
    missing = []
    if not HEX_RE.search("\n".join(line for _, line in body)):
        missing.append("提示コミットのハッシュ")
    if not any(QUOTE_RE.match(line) for _, line in body):
        missing.append("引用行")
    if missing:
        emit("NG", "仕掛かり: `## 全体設計の合意` に %s がない" % "・".join(missing))


def agreements_of(body):
    """UC 本文の合意記録を [系列 ID, 提示コミット, 引用の有無] で返す。

    系列 ID と提示コミットのハッシュが同じ行にある行を合意記録の見出しとみなし、
    次の合意記録までに引用行があれば引用ありとする。
    """
    records, current = [], None
    for line in body.splitlines():
        series, found = SERIES_RE.search(line), HEX_RE.search(line)
        if series and found:
            current = [series.group(0), found.group(0), False]
            records.append(current)
        elif current is not None and QUOTE_RE.match(line):
            current[2] = True
    return records


def verified_series(project_text):
    """第6節の合否表から、合否が記入済みの系列 ID を返す。表がなければ None。"""
    body = section_body(project_text, "## 6. 検証結果")
    table = parse_table(body) if body else None
    if table is None:
        return None
    header, rows = table
    done = set()
    for _, cells in rows:
        series = SERIES_RE.search(cell(header, cells, "UC・系列 ID"))
        result = cell(header, cells, "合否")
        if series and result not in EMPTY_CELLS and result != "未検証":
            done.add(series.group(0))
    return done


def check_progress(project_text, arch_text):
    """判定 3: 合意記録の完全性と、同時に進める系列が1本を超えていないか。"""
    if project_text is None:
        emit("対象なし", "仕掛かり: docs/project.md がない")
        return
    bodies = uc_bodies(project_text)
    if not bodies:
        emit("対象なし", "仕掛かり: docs/project.md に `### UC-n.` の本文がない")
        return
    described, agreed = set(), set()
    for uc_id, body in sorted(bodies.items(), key=lambda kv: int(kv[0][3:])):
        described |= {m.group(0) for m in SERIES_RE.finditer(body)}
        for series, commit, quoted in agreements_of(body):
            if not series.startswith(uc_id + "-"):
                emit("NG", "仕掛かり: %s の本文に他のユースケースの合意記録がある: %s" % (uc_id, series))
            elif not quoted:
                emit("NG", "仕掛かり: %s の合意記録（提示コミット %s）に利用者の応答の引用行がない"
                     % (series, commit))
            else:
                agreed.add(series)
    done = verified_series(project_text)
    if done is None:
        emit("対象なし", "仕掛かり: docs/project.md の `## 6. 検証結果` に表がない")
    else:
        wip = sorted(agreed - done)
        if len(wip) > 1:
            emit("NG", "仕掛かり: 合意済みで合否の記入がない系列が %d 本ある: %s"
                 % (len(wip), "、".join(wip)))
    if agreed:
        check_overall_agreement(arch_text)
    emit("報告", "仕掛かり: 記述済みの系列 %d 本、合意済み %d 本、合否の記入済み %s"
         % (len(described), len(agreed), "%d 本" % len(done) if done is not None else "対象なし"))


def check_uc_ids(project_text):
    """判定 4: docs/project.md の本文見出しとカタログ表で UC ID が一致するか。"""
    if project_text is None:
        emit("対象なし", "UC ID: docs/project.md がない")
        return
    head_ids = ["UC-" + m.group(1) for m in
                (UC_HEAD_RE.match(line) for line in project_text.splitlines()) if m]
    body = section_body(project_text, "## 3. ユースケースと合意")
    catalog = parse_table(body) if body else None
    if catalog is None:
        emit("対象なし", "UC ID: docs/project.md にカタログ表がない")
        return
    catalog_ids = [i for i in (uc_of(cell(catalog[0], c, "UC ID")) for _, c in catalog[1]) if i]
    ng = False
    for name, ids in (("docs/project.md 見出し", head_ids),
                      ("docs/project.md カタログ表", catalog_ids)):
        dups = sorted({i for i in ids if ids.count(i) > 1})
        if dups:
            ng = True
            emit("NG", "UC ID: %s に重複 ID がある: %s" % (name, "、".join(dups)))
    # 本文はカタログ表の部分集合でよい（着手していない UC は本文がなくてよい）。
    head_set, catalog_set = set(head_ids), set(catalog_ids)
    for uc_id in sorted(head_set - catalog_set, key=lambda x: int(x[3:])):
        ng = True
        emit("NG", "UC ID: %s の本文があるが docs/project.md カタログ表にない" % uc_id)
    if not ng:
        known = sorted(head_set | catalog_set, key=lambda x: int(x[3:]))
        emit("OK", "UC ID: %d 件の UC ID が一致している（本文あり %d 件）" % (len(known), len(head_set)))


def check_hashes(root):
    """判定 5: 輸入した標準の正規化ハッシュが期待値と一致するか。"""
    for relpath, expected in EXPECTED_HASHES.items():
        path = root / relpath
        if not path.is_file():
            emit("対象なし", "標準のハッシュ: %s がない" % relpath)
        elif norm_hash(path) == expected:
            emit("OK", "標準のハッシュ: %s は期待値と一致する" % relpath)
        else:
            emit("NG", "標準のハッシュ: %s が期待値と異なる（期待 %s、実測 %s）"
                 % (relpath, expected, norm_hash(path)))
            emit("報告", "標準のハッシュ: 差分の記録があっても報告する。")
    emit("報告", "標準のハッシュ: このスクリプト自身の正規化ハッシュは %s" % norm_hash(Path(__file__)))


def check_reports(root, project_text, arch_text):
    """判定 6: 証跡ファイル、行数、ユースケースと実現パターンの数の報告。"""
    count, total, verification = 0, 0, []
    for here, dirnames, filenames in walk(root, EVIDENCE_EXTRA_EXCLUDE):
        verification += [rel(root, here / d) for d in dirnames if d == "verification"]
        for name in filenames:
            path = here / name
            if path.suffix.lower() in EVIDENCE_EXTS:
                count, total = count + 1, total + path.stat().st_size
    emit("報告", "証跡ファイル: 該当拡張子 %d 件、合計 %d バイト、verification ディレクトリ %s"
         % (count, total, "、".join(verification) if verification else "なし"))
    for relpath, limit in LINE_LIMITS.items():
        path = root / relpath
        if not path.is_file():
            emit("対象なし", "行数: %s がない" % relpath)
            continue
        lines = len(read_text(path).splitlines())
        if lines > limit:
            emit("NG", "行数: %s は %d 行で上限 %d 行を超える" % (relpath, lines, limit))
        else:
            emit("報告", "行数: %s は %d 行（上限 %d 行）" % (relpath, lines, limit))
    if project_text is not None:
        bodies = sorted(uc_bodies(project_text).items(), key=lambda kv: int(kv[0][3:]))
        detail = "、".join("%s 拡張 %d 本" % (uc, len(set(EXT_RE.findall(body)))) for uc, body in bodies)
        emit("報告", "ユースケース: %d 件%s" % (len(bodies), "（%s）" % detail if bodies else ""))
    if arch_text is not None:
        patterns = sum(1 for line in arch_text.split("\n") if P_HEAD_RE.match(line))
        emit("報告", "実現パターン: docs/architecture.md に %d 件" % patterns)


def print_hashes(root):
    print("EXPECTED_HASHES = {")
    for relpath in EXPECTED_HASHES:
        path = root / relpath
        print('    "%s": "%s",' % (relpath, norm_hash(path) if path.is_file() else PLACEHOLDER_HASH))
    print("}")


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    parser = argparse.ArgumentParser(description="文書整合を報告する（差し戻しの判断は人が行う）。")
    parser.add_argument("root", nargs="?", default=".", help="判定対象のルート（既定はカレントディレクトリ）")
    parser.add_argument("--print-hashes", action="store_true", help="EXPECTED_HASHES のリテラルを出力する")
    args = parser.parse_args()
    root = Path(args.root).resolve()
    if args.print_hashes:
        print_hashes(root)
        return 0
    docs = {}
    for here, _, filenames in walk(root):
        for name in filenames:
            if name.lower().endswith(".md"):
                docs[(here / name).resolve()] = read_text(here / name)
    project_text = docs.get((root / "docs" / "project.md").resolve())
    arch_text = docs.get((root / "docs" / "architecture.md").resolve())
    check_links(root, docs)
    check_abs_paths(root, docs)
    check_progress(project_text, arch_text)
    check_uc_ids(project_text)
    check_hashes(root)
    check_reports(root, project_text, arch_text)
    print("NG %d 件" % NG_COUNT)
    return 1 if NG_COUNT else 0


if __name__ == "__main__":
    sys.exit(main())
