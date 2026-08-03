#!/usr/bin/env python3
"""Convert { } dashspec in test raw strings to end <kind> (ADR-0036)."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TEST_DIR = ROOT / "tests" / "DashSpec.Core.Tests"

PROP_RE = re.compile(
    r'([A-Za-z_][\w]*)\s*=\s*("(?:\\.|[^"\\])*"|[^\s;]+(?:\s+[^\s=;]+)*)'
)


def split_assignments(line: str) -> list[str]:
    line = line.strip()
    if not line:
        return []
    parts: list[str] = []
    for seg in line.split(";"):
        seg = seg.strip()
        if not seg:
            continue
        pos = 0
        while pos < len(seg):
            m = PROP_RE.search(seg, pos)
            if not m:
                if pos == 0:
                    parts.append(seg)
                break
            parts.append(m.group(0).strip())
            pos = m.end()
    return parts


def block_end_kind(header: str) -> tuple[str, str | None]:
    h = header.strip()
    if m := re.match(r"@dashboard\s+(\w+)", h):
        return "dashboard", m.group(1)
    if m := re.match(r"@tab\s+(\w+)", h):
        return "tab", m.group(1)
    if h == "report" or h.startswith("report "):
        return "report", None
    if m := re.match(r"card\s+(\w+)", h):
        return "card", m.group(1)
    if m := re.match(r"page\s+(\w+)", h):
        return "page", m.group(1)
    if m := re.match(r"tab\s+(\w+)", h):
        return "tab", m.group(1)
    if m := re.match(r"phase\s+(\w+)", h):
        return "phase", m.group(1)
    if m := re.match(r"group\s+(\w+)", h):
        return "group", m.group(1)
    if re.match(r"filter\s+(date|field|top)\s+", h):
        return "filter", None
    if h.startswith("filters host"):
        return "filters", None
    if h.startswith("filters chrome"):
        return "chrome", None
    if h.startswith("filters dashboard"):
        return "dashboard", None
    if h.startswith("filters"):
        return "filters", None
    if m := re.match(r"diagram\s+ref\s+\S+\s+(\w+)", h):
        return m.group(1), None
    if m := re.match(r"diagram\s+(\w+)", h):
        return m.group(1), None
    if m := re.match(r"^(number|bar|line|heatmap|table|palette)\b", h):
        return m.group(1), None
    if h.startswith("on click"):
        return "click", None
    if h.startswith("when "):
        return h.split()[1], None
    for kw in (
        "runtime", "configuration", "wiring", "standalone", "catalog", "legend",
        "chrome", "presentation", "transform", "limits", "export", "extensions",
        "views", "buttons", "cards", "layout board", "layout", "palette", "heatmap",
    ):
        if h == kw or h.startswith(kw + " "):
            if kw == "layout" and "board" in h:
                return "board", None
            return kw.split()[-1] if kw == "layout" else kw, None
    parts = h.split()
    return (parts[-1] if parts else "block"), None


def transform_header(header: str, base_indent: str) -> str:
    h = header.strip()
    pad = base_indent + "  "
    if m := re.match(r'report\s+"([^"]*)"\s*$', h):
        return f"{base_indent}report\n{pad}title = \"{m.group(1)}\""
    return (base_indent + h) if not header[:1].isspace() else header.rstrip()


def format_body_lines(inner: str, pad: str) -> str:
    inner = inner.strip()
    if not inner:
        return ""
    if "{" in inner:
        if not inner.startswith("\n"):
            return "\n" + inner
        return inner
    lines_out: list[str] = []
    for line in inner.splitlines():
        s = line.strip()
        if not s:
            continue
        for prop in split_assignments(s):
            lines_out.append(pad + prop)
    if not lines_out and inner:
        for prop in split_assignments(inner):
            lines_out.append(pad + prop)
    return ("\n" + "\n".join(lines_out)) if lines_out else ""


def try_special_inline(header: str, inner: str, base_indent: str) -> str | None:
    h = header.strip()
    body = inner.strip()
    if "\n" in body or "{" in body:
        return None
    if h == "bind" or h.startswith("bind "):
        items = re.split(r"[\s,]+", body)
        items = [x for x in items if x]
        if items:
            return f"{base_indent}bind\n{base_indent}  {', '.join(items)}"
        return None
    if h == "toolbar" or (h == "toolbar" and body):
        items = re.split(r"[\s,]+", body)
        items = [x for x in items if x]
        return f"{base_indent}toolbar {', '.join(items)}"
    if h == "datasource sql":
        return f"{base_indent}datasource sql {body}"
    if h.startswith("datasource sql") and body:
        return f"{base_indent}{h} {body}"
    if h == "cards":
        items = re.split(r"[\s,]+", body)
        items = [x for x in items if x]
        return (
            f"{base_indent}cards\n"
            + "\n".join(f"{base_indent}  {x}" for x in items)
            + f"\n{base_indent}end cards"
        )
    if h.startswith("filters dashboard"):
        items = [x.strip() for x in re.split(r"[,;]", body) if x.strip()]
        return (
            f"{base_indent}filters dashboard\n"
            + "\n".join(f"{base_indent}  {x}" for x in items)
            + f"\n{base_indent}end dashboard"
        )
    return None


def find_brace(s: str, start: int) -> int:
    depth = 0
    i = start
    in_str = None
    while i < len(s):
        c = s[i]
        if in_str:
            if c == "\\":
                i += 2
                continue
            if c == in_str:
                in_str = None
            i += 1
            continue
        if c in ('"', "'"):
            in_str = c
            i += 1
            continue
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return i
        i += 1
    return -1


def pad_spec_indent(spec: str, min_indent: str) -> str:
    if not min_indent:
        return spec
    out = []
    for line in spec.split("\n"):
        if not line.strip():
            out.append("")
            continue
        cur = line[: len(line) - len(line.lstrip())]
        if len(cur) < len(min_indent):
            out.append(min_indent + line.lstrip())
        else:
            out.append(line)
    return "\n".join(out)


def convert_braces(spec: str) -> str:
    while True:
        idx = -1
        in_str = None
        for i, c in enumerate(spec):
            if in_str:
                if c == in_str and (i == 0 or spec[i - 1] != "\\"):
                    in_str = None
                continue
            if c in ('"', "'"):
                in_str = c
                continue
            if c == "{":
                idx = i
                break
        if idx < 0:
            break
        close = find_brace(spec, idx)
        if close < 0:
            break
        line_start = spec.rfind("\n", 0, idx) + 1
        header = spec[line_start:idx].rstrip()
        inner = spec[idx + 1 : close]
        base_indent = re.match(r"[ \t]*", spec[line_start:]).group(0)
        inner_conv = convert_braces(inner)
        inline = try_special_inline(header, inner_conv, base_indent)
        if inline is not None:
            replacement = inline
        else:
            kind, _ = block_end_kind(header)
            new_header = transform_header(header, base_indent)
            pad = base_indent + "  "
            body = format_body_lines(inner_conv, pad)
            if "{" in inner_conv:
                body = "\n" + inner_conv if not inner_conv.startswith("\n") else inner_conv
            replacement = new_header + body + f"\n{base_indent}end {kind}"
        spec = spec[:line_start] + replacement + spec[close + 1 :]
    return spec


def migrate_spec_text(spec: str) -> str:
    if "{" not in spec:
        return spec
    return convert_braces(spec)


def should_migrate_spec(spec: str) -> bool:
    if "{" not in spec:
        return False
    return (
        "@dashboard" in spec
        or "@tab" in spec
        or "@diagram" in spec
        or re.search(r"^\s*(card|filter|diagram|number|toolbar|report|page|catalog|standalone)\s", spec, re.M)
        or "dashspec" in spec.lower()
    )


def migrate_csharp(content: str) -> str:
    pattern = re.compile(r'("""\s*\n)(.*?)(\n\s*""")', re.DOTALL)
    def sub(m: re.Match[str]) -> str:
        spec = m.group(2)
        if not should_migrate_spec(spec):
            return m.group(0)
        indent_m = re.match(r"\n(\s*)\"\"\"", m.group(3))
        min_indent = indent_m.group(1) if indent_m else ""
        converted = pad_spec_indent(migrate_spec_text(spec), min_indent)
        return m.group(1) + converted + m.group(3)
    return pattern.sub(sub, content)

def migrate_escaped_writealltext(content: str) -> str:
    def repl(m: re.Match[str]) -> str:
        raw = m.group(1)
        if "{" not in raw:
            return m.group(0)
        unescaped = raw.encode().decode("unicode_escape") if "\\n" in raw else raw
        if "{" in unescaped:
            converted = migrate_spec_text(unescaped)
            escaped = converted.replace("\\", "\\\\").replace("\n", "\\n").replace('"', '\\"')
            return f'"{escaped}"'
        return m.group(0)

    return re.sub(
        r'File\.WriteAllText\([^,]+,\s*"((?:\\.|[^"\\])*)"\)',
        repl,
        content,
    )


def migrate_writealltext_triple(content: str) -> str:
    pattern = re.compile(
        r'(File\.WriteAllText\([^,]+,\s*"""\s*\n)(.*?)(\n\s*""")',
        re.DOTALL,
    )
    def sub(m: re.Match[str]) -> str:
        spec = m.group(2)
        if not should_migrate_spec(spec):
            return m.group(0)
        indent_m = re.match(r"\n(\s*)\"\"\"", m.group(3))
        min_indent = indent_m.group(1) if indent_m else ""
        converted = pad_spec_indent(migrate_spec_text(spec), min_indent)
        return m.group(1) + converted + m.group(3)
    return pattern.sub(sub, content)


def main() -> int:
    changed = 0
    for path in sorted(TEST_DIR.glob("*.cs")):
        if path.name == "BlockSpecTestHelper.cs":
            continue
        original = path.read_text(encoding="utf-8")
        updated = migrate_writealltext_triple(migrate_csharp(original))
        if updated != original:
            path.write_text(updated, encoding="utf-8")
            print(path.name)
            changed += 1
    print(f"Updated {changed} files")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
