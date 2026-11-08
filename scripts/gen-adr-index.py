#!/usr/bin/env python3
"""Regenerate design/index.md — the ADR index — from ADR frontmatter.

OKF-style index automation: index.md is derived state, never hand-edited.
Reads design/DASHSPEC-ADR-NNNN-*.md files with `type: ADR` and rewrites
design/index.md whole (frontmatter + table + next-number hint). Files with
other types (Notes, implementation maps) are skipped.

Usage: python scripts/gen-adr-index.py [--check]
  --check  exit 1 if index.md differs from what would be generated (CI mode).
"""
from __future__ import annotations

import io
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DESIGN = ROOT / "design"
INDEX = DESIGN / "index.md"

FM = re.compile(r"\A---\n(.*?)\n---\n", re.S)
FIELD = re.compile(r"^(\w+):\s*(.+?)\s*$", re.M)
ADR_FILE = re.compile(r"^DASHSPEC-ADR-(\d{4})-.+\.md$")


def unquote(value: str) -> str:
    """`title: "…"` — strip one pair of wrapping quotes, keep inner ones."""
    value = value.strip()
    if len(value) >= 2 and value[0] == '"' and value[-1] == '"':
        return value[1:-1]
    return value


def frontmatter(path: Path) -> dict[str, str] | None:
    match = FM.match(io.open(path, encoding="utf-8").read())
    if not match:
        return None
    return {key: unquote(val) for key, val in FIELD.findall(match.group(1))}


def collect() -> list[tuple[int, str, str, str]]:
    """ADR files as (number, filename, title, status), sorted by number."""
    rows: list[tuple[int, str, str, str]] = []
    for path in sorted(DESIGN.iterdir()):
        match = ADR_FILE.match(path.name)
        if not match:
            continue
        fm = frontmatter(path)
        if not fm or fm.get("type") != "ADR":
            continue  # Notes / implementation maps are not index rows
        number = int(match.group(1))
        title = fm.get("title", "").strip() or path.stem
        status = fm.get("status", "").strip() or "Unknown"
        rows.append((number, path.name, title, status))
    return sorted(rows)


def render(rows: list[tuple[int, str, str, str]], latest_date: str) -> str:
    lines = [
        "---",
        "type: Index",
        'title: "ADR index — dash-spec"',
        f'description: "Автоматический индекс ADR ({len(rows)} записей, обновлён {latest_date}). Генерируется scripts/gen-adr-index.py из frontmatter — не редактировать руками."',
        "tags: [adr, index, dashspec]",
        "---",
        "",
        "# ADR index",
        "",
        "Производное состояние (OKF): таблица ниже собирается скриптом из frontmatter",
        f"файлов ADR. Следующий номер — **{rows[-1][0] + 1:04d}**. Ручные правки будут перезаписаны:",
        "`python scripts/gen-adr-index.py` (CI-проверка актуальности: `--check`).",
        "",
        "| ADR | Title | Status |",
        "|-----|-------|--------|",
    ]
    lines += [f"| [{n:04d}]({name}) | {title} | {status} |" for n, name, title, status in rows]
    return "\n".join(lines) + "\n"


def main() -> int:
    rows = collect()
    if not rows:
        print("no ADR files found", file=sys.stderr)
        return 1

    dates = []
    for _, name, _, _ in rows:
        fm = frontmatter(DESIGN / name) or {}
        if fm.get("date"):
            dates.append(fm["date"])
    latest_date = max(dates) if dates else ""

    generated = render(rows, latest_date)

    if INDEX.exists() and io.open(INDEX, encoding="utf-8").read() == generated:
        print(f"index.md up to date ({len(rows)} ADRs)")
        return 0
    if "--check" in sys.argv:
        print("index.md is out of date — run scripts/gen-adr-index.py", file=sys.stderr)
        return 1

    io.open(INDEX, "w", encoding="utf-8", newline="").write(generated)
    print(f"index.md regenerated: {len(rows)} ADRs, next number {rows[-1][0] + 1:04d}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
