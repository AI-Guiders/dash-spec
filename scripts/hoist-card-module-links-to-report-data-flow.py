#!/usr/bin/env python3
"""Move module [rows] links from card data flow to report-level data flow (ADR-0094)."""

from __future__ import annotations

import re
import sys
from pathlib import Path


def is_module_link(line: str) -> bool:
    s = line.strip()
    if not s or s.startswith("//"):
        return False
    if "host." in s or "chrome." in s or "[toolbar]" in s:
        return False
    if "[rows]" in s:
        return True
    return False


def is_filter_route(line: str) -> bool:
    s = line.strip()
    if not s or s.startswith("//"):
        return False
    if "->" not in s:
        return False
    if is_module_link(line):
        return False
    if "host." in s or "chrome." in s:
        return False
    return True


def to_report_link(line: str, card_id: str) -> str:
    s = line.strip()
    m = re.match(r"^(.+?)\s*->\s*(\[[^\]]+\]\s*)?(\S+)\s*$", s)
    if not m:
        return s
    left, port, slot = m.group(1), m.group(2) or "", slot
    if slot.startswith("card."):
        return s
    port = port.strip()
    if port:
        return f"{left} -> {port} card.{card_id}.{slot}"
    return f"{left} -> card.{card_id}.{slot}"


def process_file(path: Path) -> bool:
    lines = path.read_text(encoding="utf-8").splitlines()
    report_links: list[str] = []
    out: list[str] = []
    i = 0
    card_id: str | None = None
    in_card = False
    card_depth = 0

    card_start = re.compile(r"^\s*card\s+(\S+)")
    data_flow_start = re.compile(r"^(\s*)data flow\s*$")
    data_flow_end = re.compile(r"^(\s*)end data flow\s*$")

    while i < len(lines):
        line = lines[i]
        cm = card_start.match(line)
        if cm and not line.strip().startswith("//"):
            card_id = cm.group(1)
            in_card = True
            card_depth = len(line) - len(line.lstrip())
        if in_card and line.strip() == "end card":
            in_card = False
            card_id = None

        dm = data_flow_start.match(line)
        if in_card and card_id and dm:
            indent = dm.group(1)
            body: list[str] = []
            i += 1
            while i < len(lines):
                if data_flow_end.match(lines[i]):
                    break
                body.append(lines[i])
                i += 1
            kept: list[str] = []
            for bl in body:
                if is_module_link(bl):
                    report_links.append(to_report_link(bl, card_id))
                elif is_filter_route(bl):
                    kept.append(bl)
                else:
                    kept.append(bl)
            if kept:
                out.append(f"{indent}data flow")
                out.extend(kept)
                out.append(f"{indent}end data flow")
            i += 1
            continue

        out.append(line)
        i += 1

    if not report_links:
        return False

    # insert report data flow after first "report" block line with title or after report line
    inserted = False
    final: list[str] = []
    for idx, line in enumerate(out):
        final.append(line)
        if not inserted and re.match(r"^\s*report\b", line):
            # wait for a stable anchor: after defaults block or after title
            continue
        if not inserted and report_links and re.match(r"^\s*page\s+", line):
            final.insert(-1, "  data flow")
            for rl in report_links:
                final.insert(-1, f"    {rl}")
            final.insert(-1, "  end data flow")
            inserted = True

    if not inserted:
        # fallback: after `report` line block begin - find line `  title` or first `  page` / `  card`
        final = []
        for idx, line in enumerate(out):
            final.append(line)
            if not inserted and line.strip().startswith("title ="):
                final.append("")
                final.append("  data flow")
                for rl in report_links:
                    final.append(f"    {rl}")
                final.append("  end data flow")
                inserted = True

    path.write_text("\n".join(final) + "\n", encoding="utf-8")
    return True


def main(argv: list[str]) -> int:
    for arg in argv[1:]:
        path = Path(arg)
        files = list(path.rglob("*.dashspec")) if path.is_dir() else [path]
        for f in files:
            if process_file(f):
                print(f"hoisted {f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv))
