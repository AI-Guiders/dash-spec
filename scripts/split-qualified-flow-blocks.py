#!/usr/bin/env python3
"""Split legacy `flow` / `end flow` into data|show|wire|action qualified blocks."""

from __future__ import annotations

import re
import sys
from pathlib import Path


def classify_line(line: str) -> str:
    stripped = line.strip()
    if not stripped or stripped.startswith("//"):
        return "skip"
    lower = stripped.lower()
    if "host." in lower or "host.chrome" in lower:
        return "wire"
    if "[toolbar]" in lower or "[panel]" in lower or "[filters]" in lower:
        return "show"
    if "chrome." in lower:
        return "show"
    if "[click" in lower or "phase." in lower or lower.startswith("page."):
        return "action"
    return "data"


def transform_block(body_lines: list[str], indent: str) -> list[str]:
    buckets: dict[str, list[str]] = {"data": [], "show": [], "wire": [], "action": []}
    for line in body_lines:
        kind = classify_line(line)
        if kind == "skip":
            continue
        buckets[kind].append(line)

    out: list[str] = []
    for kind in ("show", "wire", "data", "action"):
        lines = buckets[kind]
        if not lines:
            continue
        out.append(f"{indent}{kind} flow")
        out.extend(lines)
        out.append(f"{indent}end {kind} flow")
    return out


def transform_text(text: str) -> str:
    lines = text.splitlines()
    i = 0
    out: list[str] = []
    flow_re = re.compile(r"^(\s*)flow\s*$")

    while i < len(lines):
        m = flow_re.match(lines[i])
        if not m:
            out.append(lines[i])
            i += 1
            continue

        indent = m.group(1)
        i += 1
        body: list[str] = []
        end_re = re.compile(rf"^{indent}end flow\s*$")
        while i < len(lines) and not end_re.match(lines[i]):
            body.append(lines[i])
            i += 1
        if i >= len(lines):
            raise ValueError("unclosed flow block")
        i += 1  # skip end flow
        out.extend(transform_block(body, indent))

    return "\n".join(out) + ("\n" if text.endswith("\n") else "")


def main(argv: list[str]) -> int:
    if len(argv) < 2:
        print("usage: split-qualified-flow-blocks.py <file-or-dir> ...", file=sys.stderr)
        return 2
    for arg in argv[1:]:
        path = Path(arg)
        paths = list(path.rglob("*.dashspec")) if path.is_dir() else [path]
        for file in paths:
            original = file.read_text(encoding="utf-8")
            updated = transform_text(original)
            if updated != original:
                file.write_text(updated, encoding="utf-8")
                print(f"updated {file}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
