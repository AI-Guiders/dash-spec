# DashSpec.Presentation

Shared Blazor Razor Class Library for **DashSpec.Host** (web viewer) and **DashSpec Studio** (Report Preview).

## Status

Phase **II3** scaffold ([DASHSPEC-ADR-0047](../design/DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) §6, [DASHSPEC-ADR-0051](../design/DASHSPEC-ADR-0051-language-affinity-modeling-execution.md) §4b).

| Milestone | Scope |
|-----------|-------|
| **Now** | RCL shell + `RichTextView` (Creole-subset, [ADR-0005](../../design/DASHSPEC-ADR-0005-rich-text-creole-subset.md)) |
| **Next** | Extract chart/card/filter components from Host |
| **Gate** | Studio v0 references this package — not `DashSpec.Host` |

## Consumers

- `DashSpec.Host` — consumption viewer shell (thin)
- `dash-spec-studio` — desktop authoring + Report Preview (Phase III1)

Session parity (same spec → same payload hash) is enforced via `IReportPreviewSession` in `DashSpec.Abstractions` and Execution.Runtime session extract — not WebView2 embed of Host URL.

See [DASHSPEC-II3-presentation-scaffold.md](../design/DASHSPEC-II3-presentation-scaffold.md).
