"""Post-fix migrated dashspec strings in DashSpec.Core.Tests."""
from __future__ import annotations

import importlib.util
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TEST = ROOT / "tests" / "DashSpec.Core.Tests"
MIG = ROOT / "scripts" / "migrate-tests-braces-to-end.py"

spec = importlib.util.spec_from_file_location("mig", MIG)
mig = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mig)

PROP_RE = re.compile(
    r"([A-Za-z_][\w]*)\s*=\s*(\"(?:\\.|[^\"\\])*\"|[^\s;]+(?:\s+[^\s=;]+)*)"
)


def split_prop_line(line: str) -> list[str]:
    s = line.strip()
    if not s or "=" not in s:
        return [line] if line.strip() else []
    parts: list[str] = []
    pos = 0
    while pos < len(s):
        m = PROP_RE.search(s, pos)
        if not m:
            break
        parts.append(m.group(0).strip())
        pos = m.end()
    return parts if len(parts) > 1 else [s]


def split_inline_props_in_text(text: str) -> str:
  lines_out: list[str] = []
  for line in text.split("\n"):
    stripped = line.lstrip()
    if not stripped:
      lines_out.append(line)
      continue
    indent = line[: len(line) - len(stripped)]
    # diagram/layout property lines (not directives)
    if re.match(r"^(x|y|value|tooltip|category|series|columns|gap|row|col|width|height|cells|axis)\s*=", stripped):
      props = split_prop_line(stripped)
      if len(props) > 1:
        for p in props:
          lines_out.append(indent + p)
        continue
    if re.match(r"^columns = \d+ gap", stripped):
      props = split_prop_line(stripped)
      for p in props:
        lines_out.append(indent + p)
      continue
    lines_out.append(line)
  return "\n".join(lines_out)


def fix_layout_grid_end(text: str) -> str:
  # layout grid block should end with end grid, not end layout
  lines = text.split("\n")
  out: list[str] = []
  i = 0
  while i < len(lines):
    line = lines[i]
    if re.search(r"\blayout\s+grid\s*$", line.strip()) or line.strip() == "layout grid":
      out.append(line)
      i += 1
      depth = 1
      while i < len(lines) and depth > 0:
        s = lines[i].strip()
        if re.match(r"layout\s+(board|grid)", s):
          depth += 1
        if s == "end layout" and depth == 1:
          indent = lines[i][: len(lines[i]) - len(lines[i].lstrip())]
          out.append(indent + "end grid")
          i += 1
          depth -= 1
          continue
        if s.startswith("end "):
          if s == "end grid":
            depth -= 1
        out.append(lines[i])
        i += 1
      continue
    # wiring layout grid ... end layout -> end grid (single level)
    out.append(line)
    i += 1
  text2 = "\n".join(out)
  # simpler global: after "layout grid" section, first "end layout" at same indent -> end grid
  return re.sub(
    r"(^(\s*)layout grid\s*\n(?:.*\n)*?^\2)end layout\s*$",
    r"\1end grid",
    text2,
    flags=re.M,
  )


def fix_bind_multiline(text: str) -> str:
  # bind\n  names without end bind -> add end bind if parser needs block form
  # User said: multiline bind needs end bind
  def repl(m: re.Match[str]) -> str:
    ind = m.group(1)
    body = m.group(2).strip()
    if "end bind" in text[m.end() : m.end() + 80]:
      return m.group(0)
    return f"{ind}bind\n{ind}  {body}\n{ind}end bind"
  return re.sub(r"^(\s*)bind\s*\n\s*([\w, ]+)\s*$", repl, text, flags=re.M)


def fix_filters_dashboard_inline(text: str) -> str:
  return re.sub(
    r"filters dashboard \{([^}]+)\}",
    lambda m: "filters dashboard\n" + "\n".join(
      f"  {x.strip()}" for x in re.split(r"[,;]", m.group(1)) if x.strip()
    ) + "\nend dashboard",
    text,
  )


def migrate_braces_in_text(text: str) -> str:
  if "{" not in text:
    return text
  return mig.convert_braces(text)


def fix_filter_date_lines(text: str) -> str:
  # broken: filter date X on Y as "Z" -7d..today (missing default =)
  text = re.sub(
    r'(filter date \w+ on [^\n]+ as "[^"]+") -7d\.\.today',
    r'\1 default -7d..today',
    text,
  )
  return text


def process_spec(spec: str) -> str:
  if "{" in spec:
    spec = migrate_braces_in_text(spec)
  spec = fix_filters_dashboard_inline(spec)
  spec = split_inline_props_in_text(spec)
  spec = fix_layout_grid_end(spec)
  spec = fix_bind_multiline(spec)
  spec = fix_filter_date_lines(spec)
  return spec


RAW = re.compile(r'(\$?\$?"""\s*\n)(.*?)(\n(\s*)""")', re.S)


def process_cs(content: str) -> str:
  def sub(m: re.Match[str]) -> str:
    body = m.group(2)
    if "@" not in body and "diagram" not in body and "filter" not in body:
      return m.group(0)
    conv = process_spec(body)
    conv = mig.pad_spec_indent(conv, m.group(4))
    return m.group(1) + conv + m.group(3)

  content = RAW.sub(sub, content)
  # one-line WriteAllText escaped
  def esc_repl(m: re.Match[str]) -> str:
    raw = m.group(1)
    if "{" not in raw:
      return m.group(0)
    inner = raw.replace("\\n", "\n").replace('\\"', '"')
    conv = process_spec(inner)
    esc = conv.replace("\\", "\\\\").replace("\n", "\\n").replace('"', '\\"')
    return f'"{esc}"'
  content = re.sub(
    r'File\.WriteAllText\([^,]+,\s*"((?:\\.|[^"\\])*)"\)',
    esc_repl,
    content,
  )
  return content


def main() -> None:
  for path in sorted(TEST.glob("*.cs")):
    if path.name == "BlockSpecTestHelper.cs":
      continue
    orig = path.read_text(encoding="utf-8")
    upd = process_cs(orig)
    if upd != orig:
      path.write_text(upd, encoding="utf-8")
      print(path.name)


if __name__ == "__main__":
  main()
