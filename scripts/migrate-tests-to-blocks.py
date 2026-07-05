#!/usr/bin/env python3
"""Migrate legacy dashboard/tab .dashspec snippets in C# test raw strings to ADR-0024 blocks."""

from __future__ import annotations

import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TEST_DIR = ROOT / "tests" / "DashSpec.Core.Tests"

DASHBOARD_OPEN = re.compile(
    r"(@dashboard\s+(\w+)\s*)\n(\s*)dashboard\s+\"([^\"]+)\"\s*\{",
    re.MULTILINE,
)

TAB_LEGACY_OPEN = re.compile(
    r"(@tab\s+(\w+)\s*)\n(?!\s*\{)",
    re.MULTILINE,
)

RUNTIME = re.compile(r"^\s*@runtime\s+\"([^\"]+)\"\s*$", re.MULTILINE)
CONFIG = re.compile(r"^\s*@config\s+\"([^\"]+)\"\s*$", re.MULTILINE)
SQLDIALECT = re.compile(r"^\s*@sqldialect\s+(\w+)\s*$", re.MULTILINE)
PALETTE_FILE = re.compile(r"^\s*@palette\s+\"([^\"]+)\"\s*$", re.MULTILINE)
DIAGRAMLIB = re.compile(r"^\s*@diagramlibrary\s+\"([^\"]+)\"\s*$", re.MULTILINE)

TAB_INNER = re.compile(
    r"^\s*tab\s+(\w+)(?:\s+as\s+\"([^\"]+)\")?\s*\{\s*\n\s*layout\s*\{",
    re.MULTILINE,
)

PARSE_MARKER = 'DashSpecParser.Parse("""'


def strip_directives(text: str) -> tuple[str, dict]:
    meta: dict = {}
    for pat, key in ((RUNTIME, "runtime"), (CONFIG, "runtime"), (SQLDIALECT, "sqldialect")):
        m = pat.search(text)
        if m:
            meta[key] = m.group(1)
            text = pat.sub("", text, count=1)
    for pat, key in ((PALETTE_FILE, "palette_path"), (DIAGRAMLIB, "diagramlibrary")):
        m = pat.search(text)
        if m:
            meta[key] = m.group(1)
            text = pat.sub("", text, count=1)
    return text, meta


def envelope_prefix(meta: dict, indent: str) -> str:
    lines: list[str] = []
    if "runtime" in meta:
        lines.append(f'{indent}runtime {{ manifest = "{meta["runtime"]}" }}')
    cfg: list[str] = []
    if "sqldialect" in meta:
        cfg.append(f"sqldialect = {meta['sqldialect']}")
    if "palette_path" in meta:
        cfg.append(f'palette = "{meta["palette_path"]}"')
    if "diagramlibrary" in meta:
        cfg.append(f'diagramlibrary = "{meta["diagramlibrary"]}"')
    if cfg:
        lines.append(f"{indent}configuration {{ {' '.join(cfg)} }}")
    if not lines:
        return ""
    return "\n".join(lines) + "\n"


def balance_block_closes(text: str, dashboard_brace_index: int) -> str:
    depth = 0
    for j in range(dashboard_brace_index, len(text)):
        if text[j] == "{":
            depth += 1
        elif text[j] == "}":
            depth -= 1
    if depth <= 0:
        return text

    dash_line_start = text.rfind("\n", 0, dashboard_brace_index) + 1
    indent = re.match(r"[ \t]*", text[dash_line_start:]).group(0)
    trimmed = text.rstrip()
    extra = "\n".join(f"{indent}}}" for _ in range(depth))
    return f"{trimmed}\n{extra}\n"


def migrate_dashboard_block(text: str) -> str:
    if not DASHBOARD_OPEN.search(text):
        return text

    leading = len(text) - len(text.lstrip("\n"))
    lead = text[:leading]
    body = text[leading:]
    body, meta = strip_directives(body)

    def repl(m: re.Match[str]) -> str:
        prefix, _id, indent, title = m.group(1), m.group(2), m.group(3), m.group(4)
        env = envelope_prefix(meta, indent + "  ")
        return f"{prefix} {{\n{env}{indent}  report \"{title}\" {{"

    body = DASHBOARD_OPEN.sub(repl, body)
    idx = body.find("@dashboard")
    brace = body.find("{", idx)
    body = balance_block_closes(body, brace)
    return lead + body


def migrate_tab_legacy_block(text: str) -> str:
    if not TAB_LEGACY_OPEN.search(text):
        return text
    if re.search(r"@tab\s+\w+\s*\{", text):
        return text

    leading = len(text) - len(text.lstrip("\n"))
    lead = text[:leading]
    body = text[leading:]
    body, meta = strip_directives(body)

    m = TAB_LEGACY_OPEN.search(body)
    if not m:
        return text

    tab_id = m.group(2)
    indent = re.match(r"^(\s*)", m.group(0).split("\n")[0] or "").group(1)

    layout_board: str | None = None
    report_title = tab_id
    inner = TAB_INNER.search(body)
    if inner:
        tab_id = inner.group(1)
        if inner.group(2):
            report_title = inner.group(2)
        start = inner.end() - len("layout {")
        depth = 0
        end = start
        for j in range(start, len(body)):
            if body[j] == "{":
                depth += 1
            elif body[j] == "}":
                depth -= 1
                if depth == 0:
                    end = j + 1
                    break
        layout_body = body[start:end]
        layout_board = layout_body.replace("layout {", "layout board {", 1)
        tab_block_end = end
        while tab_block_end < len(body) and body[tab_block_end] in " \t\n":
            tab_block_end += 1
        if tab_block_end < len(body) and body[tab_block_end] == "}":
            tab_block_end += 1
        body = body[: inner.start()] + body[tab_block_end:]

    body = TAB_LEGACY_OPEN.sub("", body, count=1).lstrip("\n")

    wiring_lines: list[str] = []
    conn = re.search(r"^\s*connector\s+(\w+)", body, re.MULTILINE)
    if conn:
        wiring_lines.append(f"use connector {conn.group(1)}")
        body = re.sub(r"^\s*connector\s+\w+\s*\n", "", body, count=1, flags=re.MULTILINE)

    if layout_board:
        wiring_lines.append(layout_board.strip())

    env = envelope_prefix(meta, indent + "  ")
    wiring = ""
    if wiring_lines:
        inner_w = "\n    ".join(wiring_lines)
        wiring = f"{indent}  wiring {{\n    {inner_w}\n{indent}  }}\n"

    standalone = ""
    if re.search(r"^\s*filter\s+", body, re.MULTILINE) or re.search(r"^\s*toolbar\s*\{", body, re.MULTILINE):
        body_lines: list[str] = []
        remaining: list[str] = []
        in_toolbar = False
        depth = 0
        for line in body.split("\n"):
            stripped = line.strip()
            if not in_toolbar and (stripped.startswith("filter ") or stripped.startswith("toolbar ")):
                body_lines.append(line)
                if stripped.startswith("toolbar ") and "{" in stripped:
                    in_toolbar = True
                    depth = stripped.count("{") - stripped.count("}")
                continue
            if in_toolbar:
                body_lines.append(line)
                depth += line.count("{") - line.count("}")
                if depth <= 0:
                    in_toolbar = False
                continue
            remaining.append(line)
        if body_lines:
            standalone_body = "\n".join(body_lines)
            standalone = (
                f"{indent}  report \"{report_title}\" {{\n"
                f"{indent}    standalone {{\n"
                f"{standalone_body}\n"
                f"{indent}    }}\n"
            )
            body = "\n".join(remaining)

    if not standalone:
        standalone = f'{indent}  report "{report_title}" {{\n'

    rebuilt = (
        f"{indent}@tab {tab_id} {{\n"
        f"{env}"
        f"{wiring}"
        f"{standalone}"
        f"{body.rstrip()}\n"
        f"{indent}  }}\n"
        f"{indent}}}\n"
    )
    return lead + rebuilt


def migrate_tab_module_file(text: str) -> str:
    lines = text.strip().split("\n")
    if not lines or not lines[0].strip().startswith("@tab "):
        return text
    if lines[0].strip().endswith("{"):
        return text

    tab_id = lines[0].split()[1]
    body = "\n".join(lines[1:]).strip()
    body = re.sub(
        r"^\s*tab\s+\w+(?:\s+as\s+\"[^\"]+\")?\s*\{\s*\n?",
        "",
        body,
        count=1,
        flags=re.MULTILINE,
    )
    body = re.sub(r"\n\s*\}\s*$", "", body.rstrip())
    return f"@tab {tab_id} {{\n  report {{\n{body}\n  }}\n}}\n"


def migrate_write_all_text_dashspec(content: str) -> str:
    pattern = re.compile(
        r'(File\.WriteAllText\(Path\.Combine\([^)]+\),\s*"""\s*\n)(@dashboard\s+\w+\s*\n\s*dashboard\s+"[^"]+"\s*\{.*?)(\n\s*"""\))',
        re.DOTALL,
    )

    def repl(m: re.Match[str]) -> str:
        spec = migrate_dashboard_block(m.group(2))
        return m.group(1) + spec + m.group(3)

    return pattern.sub(repl, content)


def migrate_csharp(content: str) -> str:
    content = migrate_write_all_text_dashspec(content)
    result: list[str] = []
    i = 0
    while True:
        idx = content.find(PARSE_MARKER, i)
        if idx < 0:
            result.append(content[i:])
            break
        result.append(content[i:idx])
        start = idx + len(PARSE_MARKER)
        end = content.find('"""', start)
        if end < 0:
            result.append(content[idx:])
            break
        spec = content[start:end]
        if "@dashboard" in spec and 'dashboard "' in spec:
            spec = migrate_dashboard_block(spec)
        elif "@tab" in spec and not re.search(r"@tab\s+\w+\s*\{", spec):
            spec = migrate_tab_legacy_block(spec)
        result.append(PARSE_MARKER + spec + '"""')
        i = end + 3
    return "".join(result)


def migrate_write_tab_module(content: str) -> str:
    pattern = re.compile(
        r'File\.WriteAllText\(Path\.Combine\(dir, "[^"]+\.dashspec"\),\s*"""\s*\n\s*@tab\s+(\w+)\s*\n(.*?)\n\s*"""\)',
        re.DOTALL,
    )

    def repl(m: re.Match[str]) -> str:
        raw = f"@tab {m.group(1)}\n{m.group(2)}"
        converted = migrate_tab_module_file(raw)
        return f'File.WriteAllText(Path.Combine(dir, "{m.group(1)}.dashspec"), """\n{converted}""")'

    return pattern.sub(repl, content)


def main() -> int:
    changed = 0
    for path in sorted(TEST_DIR.glob("*.cs")):
        if path.name == "BlockSpecTestHelper.cs":
            continue
        original = path.read_text(encoding="utf-8")
        updated = migrate_csharp(original)
        updated = migrate_write_tab_module(updated)
        if updated != original:
            path.write_text(updated, encoding="utf-8")
            print(f"Updated {path.name}")
            changed += 1
    print(f"Done. {changed} file(s) updated.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
