#!/usr/bin/env python3
"""Convert filter toolbar show-flow lines to placement flow (ADR-0096)."""

from __future__ import annotations

import re
import sys
from pathlib import Path


def migrate_block(body: str, indent: str) -> str | None:
    lines = []
    for raw in body.splitlines():
        line = raw.strip()
        if not line or line.startswith("//"):
            continue
        m = re.match(
            r"^(\w+)\s*->\s*\[toolbar\]\s+chrome\.dashboard\s*$", line, re.I
        )
        if m:
            lines.append(f"{indent}  {m.group(1)} -> report")
            continue
        m = re.match(
            r"^(\w+)\s*->\s*\[toolbar\]\s+chrome\.page\.(\S+)\s*$", line, re.I
        )
        if m:
            lines.append(f"{indent}  {m.group(1)} -> page.{m.group(2)}")
            continue
        m = re.match(
            r"^(\w+)\s*->\s*\[toolbar\]\s+chrome\.card\.(\S+)\s*$", line, re.I
        )
        if m:
            lines.append(f"{indent}  {m.group(1)} -> card.{m.group(2)}")
            continue
        return None
    if not lines:
        return None
    return "\n".join(
        [f"{indent}placement flow", *lines, f"{indent}end placement flow"]
    )


def transform(text: str) -> str:
    out: list[str] = []
    i = 0
    lines = text.splitlines()
    show_start = re.compile(r"^(\s*)show flow\s*$")
    show_end = re.compile(r"^(\s*)end show flow\s*$")

    while i < len(lines):
        m = show_start.match(lines[i])
        if not m:
            out.append(lines[i])
            i += 1
            continue
        indent = m.group(1)
        i += 1
        body_lines: list[str] = []
        while i < len(lines) and not show_end.match(lines[i]):
            body_lines.append(lines[i])
            i += 1
        if i >= len(lines):
            raise ValueError("unclosed show flow")
        i += 1
        body = "\n".join(body_lines)
        placement = migrate_block(body, indent)
        if placement:
            out.append(placement)
        else:
            out.append(f"{indent}show flow")
            out.extend(body_lines)
            out.append(f"{indent}end show flow")

    return "\n".join(out) + ("\n" if text.endswith("\n") else "")


def main(argv: list[str]) -> int:
    paths = []
    for arg in argv[1:]:
        p = Path(arg)
        paths.extend(p.rglob("*.dashspec") if p.is_dir() else [p])
    for path in paths:
        original = path.read_text(encoding="utf-8")
        updated = transform(original)
        if updated != original:
            path.write_text(updated, encoding="utf-8")
            print(path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
