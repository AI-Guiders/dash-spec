#!/usr/bin/env python3
"""Сравнивает форму C#-модели и F#-IR: что совпадает, что расходится.

Нужен для планирования волн миграции ADR-0048 («C# читает F#-модель напрямую»).
Совпадающие типы можно перевести алиасом без правок кода; расходящиеся — по списку.

Запуск из корня репозитория:
    python tools/model-shape-diff.py            # отчёт в консоль
    python tools/model-shape-diff.py --json     # машиночитаемый отчёт
"""

from __future__ import annotations

import argparse
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent.parent
CS_MODEL = ROOT / "src" / "DashSpec.Core" / "Model"
FS_ROOTS = [
    ROOT / "src" / "DashSpec.Modeling.Parse",
    ROOT / "src" / "DashSpec.Modeling.Core",
]

# C#-тип -> F#-тип, когда имена расходятся (см. отчёт разведки).
RENAMES = {
    "PaletteDefinition": "PaletteDocument",
    "CardDiagramSlotDefinition": "CardDiagramSlot",
    "CardFlowInputDefinition": "CardFlowInput",
    "DashflowModuleDefinition": "DashflowModule",
    "DashflowSourceDefinition": "DashflowSourceDef",
    "DashflowTransformParameterDefinition": "DashflowTransformParam",
    "DashflowTransformStepDefinition": "DashflowTransformStepDef",
    "DashflowTransformerDefinition": "DashflowTransformerDef",
    "DashflowLinkDefinition": "FlowLinkDef",
    "DashflowSourceFromKind": "SourceFromKind",
}

# Типы, которые не модель, а логика/зеркала case'ов: остаются в C# (Execution) либо удаляются.
NOT_MODEL = {
    "CardClickRefreshPlanner", "CardInteractionRefresh",
    "CategoryAxisOrderParser", "CategoryAxisOrdering", "CategorySortMode", "CategoryAxis",
    "VizAxisPresentationParser", "VizLabelDisplayResolver", "IVizAxisPresentationSpec",
    "VizValueLabelMode", "MatrixAxisScrollMode", "HeatmapCellContext",
    "SeriesTransformSettings", "DiagramKindInfo", "DisplayBindingDefinition",
    "DashflowExecutionPath",
}
MIRRORS_DU_CASES = {
    "ShowSelectionEffect", "SetFilterFromFieldEffect", "DrillTableFromCellEffect",
    "InvokeHandlerEffect", "GotoTabEffect", "FocusPhaseEffect", "GotoPageEffect",
    "GotoCatalogEntryEffect", "LayoutBoardCardRow", "LayoutBoardGroupRow", "LayoutBoardNestRow",
}


def cs_types() -> dict[str, tuple[str, list[tuple[str, str]]]]:
    """C#-типы модели: имя -> (имя файла, [(поле, тип)])."""
    out: dict[str, tuple[str, list[tuple[str, str]]]] = {}
    for path in sorted(CS_MODEL.glob("*.cs")):
        text = path.read_text(encoding="utf-8")
        for match in re.finditer(
            r"public\s+sealed\s+(?:record|class)\s+([A-Za-z_][A-Za-z0-9_]*)\s*(?:\(([^;]*?)\))?\s*(?:\{|;)",
            text,
            re.S,
        ):
            name, body = match.group(1), match.group(2)
            fields: list[tuple[str, str]] = []
            if body:
                depth = 0
                current = ""
                parts = []
                for ch in body:
                    if ch in "(<[":
                        depth += 1
                    elif ch in ")>]":
                        depth -= 1
                    if ch == "," and depth == 0:
                        parts.append(current)
                        current = ""
                    else:
                        current += ch
                parts.append(current)
                for part in parts:
                    part = part.strip()
                    if not part or "=" in part.split("?")[0].replace("=>", ""):
                        pass
                    m = re.match(r"^([\w<>,\[\]?\s]+?)\s+([A-Za-z_]\w*)\s*(=|$)", part)
                    if m:
                        fields.append((m.group(2), m.group(1).strip()))
            out[name] = (path.name, fields)
    return out


def fs_types() -> dict[str, tuple[str, str, list[tuple[str, str]]]]:
    """F#-типы: имя -> (файл, namespace, [(поле, тип)]).

    F#-запись обычно занимает несколько строк, причём `{` стоит на отдельной строке:
        type FilterDefinition =
            { Kind: FilterKind
              Name: string }
    """
    out: dict[str, tuple[str, str, list[tuple[str, str]]]] = {}
    for root in FS_ROOTS:
        for path in sorted(root.rglob("*.fs")):
            text = path.read_text(encoding="utf-8")
            ns = ""
            lines = text.splitlines()
            index = 0
            while index < len(lines):
                line = lines[index]
                ns_match = re.match(r"\s*namespace\s+([\w.]+)", line)
                if ns_match:
                    ns = ns_match.group(1)
                    index += 1
                    continue
                decl = re.match(r"\s*(?:type|and)\s+([A-Za-z_]\w*)\s*=(.*)$", line)
                if not decl:
                    index += 1
                    continue
                name, tail = decl.group(1), decl.group(2)
                # Тело записи: либо на этой же строке, либо со следующей.
                body_lines: list[str] = []
                cursor = index
                if "{" in tail:
                    body_lines.append(tail.split("{", 1)[1])
                else:
                    cursor += 1
                    while cursor < len(lines) and not lines[cursor].strip():
                        cursor += 1
                    if cursor < len(lines) and lines[cursor].lstrip().startswith("{"):
                        body_lines.append(lines[cursor].split("{", 1)[1])
                    else:
                        index += 1
                        continue
                fields: list[tuple[str, str]] = []
                closed = False
                while cursor < len(lines) and not closed:
                    chunk = body_lines.pop(0) if body_lines else None
                    if chunk is None:
                        cursor += 1
                        if cursor >= len(lines):
                            break
                        chunk = lines[cursor]
                    for piece in chunk.split("}"):
                        piece = piece.strip()
                        if not piece:
                            continue
                        if "}" in chunk:
                            closed = True
                        fm = re.match(r"^([A-Za-z_]\w*)\s*:\s*(.+?)\s*$", piece)
                        if fm:
                            fields.append((fm.group(1), fm.group(2)))
                    if "}" in chunk:
                        closed = True
                if fields:
                    out[name] = (path.name, ns, fields)
                index = max(cursor, index) + 1
    return out


def norm(text: str, is_fs: bool) -> str:
    t = text.strip()
    if is_fs:
        t = re.sub(r"^DashSpec\.Modeling\.Parse\.(\w+\.)*", "", t)
        t = t.replace(" option", "?")
    t = t.replace("System.Collections.Generic.", "")
    t = t.replace("IReadOnlyList<", "list<").replace("IReadOnlyDictionary<", "map<")
    t = t.replace("IReadOnlySet<", "set<").replace("FSharpOption<", "opt<")
    t = re.sub(r"\s+", " ", t)
    return t


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--json", action="store_true")
    args = parser.parse_args()

    cs = cs_types()
    fs = fs_types()
    report = []
    for cs_name, (cs_file, cs_fields) in sorted(cs.items()):
        if cs_name in MIRRORS_DU_CASES:
            report.append({"cs": cs_name, "kind": "mirror-of-du-case", "cs_file": cs_file})
            continue
        if cs_name in NOT_MODEL:
            report.append({"cs": cs_name, "kind": "logic-not-model", "cs_file": cs_file})
            continue
        fs_name = RENAMES.get(cs_name, cs_name)
        entry = fs.get(fs_name)
        if entry is None:
            report.append({"cs": cs_name, "kind": "no-fsharp-twin", "cs_file": cs_file})
            continue
        fs_file, ns, fs_fields = entry
        cs_map = {f[0]: norm(f[1], False) for f in cs_fields}
        fs_map = {f[0]: norm(f[1], True) for f in fs_fields}
        only_cs = sorted(set(cs_map) - set(fs_map))
        only_fs = sorted(set(fs_map) - set(cs_map))
        type_diff = sorted(
            k for k in set(cs_map) & set(fs_map) if cs_map[k] != fs_map[k]
        )
        option_fields = sorted(f[0] for f in fs_fields if f[1].strip().endswith("option"))
        report.append({
            "cs": cs_name,
            "fsharp": f"{ns}.{fs_name}",
            "cs_file": cs_file,
            "fs_file": fs_file,
            "kind": "identical" if not (only_cs or only_fs or type_diff) else "differs",
            "only_cs": only_cs,
            "only_fsharp": only_fs,
            "type_diff": type_diff,
            "option_fields": option_fields,
        })

    if args.json:
        print(json.dumps(report, ensure_ascii=False, indent=2))
        return 0

    groups: dict[str, list] = {}
    for row in report:
        groups.setdefault(row["kind"], []).append(row)
    for kind in ("identical", "differs", "mirror-of-du-case", "logic-not-model", "no-fsharp-twin"):
        rows = groups.get(kind, [])
        print(f"\n### {kind} — {len(rows)}")
        for row in rows:
            if kind == "identical":
                opts = row.get("option_fields") or []
                mark = "  ← БЕЗ option-полей: переводится без правок кода" if not opts else ""
                print(f"  {row['cs']:<38} -> {row['fsharp']:<62} option-полей: {len(opts)}{mark}")
            elif kind == "differs":
                print(
                    f"  {row['cs']:<38} -> {row['fsharp']:<60} "
                    f"только в C#: {row['only_cs']}, только в F#: {row['only_fsharp']}, "
                    f"типы: {row['type_diff']}"
                )
            else:
                print(f"  {row['cs']:<38} ({row['cs_file']})")
    total = len(report)
    same = len(groups.get("identical", []))
    print(f"\nВСЕГО {total}: совпадают {same}, расходятся {total - same - len(groups.get('mirror-of-du-case', [])) - len(groups.get('logic-not-model', [])) - len(groups.get('no-fsharp-twin', []))}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
