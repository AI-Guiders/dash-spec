# DASHSPEC-ADR-0098: Compiler modules — `namespace` and `import`

| | |
|---|---|
| **Status** | Accepted · **implemented** (`IReportCompiler`, `dashspec.toml` index, `import`; `samples/demo` + `tests/Fixtures/import-project`; legacy `!include` in module headers rejected when `disable_legacy_includes`; planet LUS — ADR-0099 B5) |
| **Date** | 2026-10-07 |
| **Relates to** | [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md), [ADR-0097](DASHSPEC-ADR-0097-document-compile-pipeline.md) |

## Context

DashSpec is a **multi-root DSL** (many file extensions, `!include`, globs) linked at parse time ([ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md)). [ADR-0097](DASHSPEC-ADR-0097-document-compile-pipeline.md) added a **document compile pipeline** but not a **module graph** or symbol table.

Goal: a **compiler** front-end (namespaces + kind-filtered imports) and the existing pipeline as middle-end. Keyword **`import`** is already reserved in the lexer; it fits scripting/declarative DSLs better than C#-style `using`.

## Decision (v1 — language; implementation phased)

### Compiler shape

| Stage | Responsibility |
|-------|----------------|
| **Front** | Parse units → **symbol table** (`namespace` + `import`) → link into registries (replaces `!include`) |
| **Middle** | `DocumentCompilePipeline`: `compileScopeFlows` → document phases |
| **Back** | `DashboardDocument`, `WiringGraph`, diagnostics, optional emitted bundle |

Entry: `@dashboard` / `@tab` module (`.dashspec` today; `.dspec` rename is optional follow-up).

**Report body blocks** (`report`, `card`, `data flow`, …) are unchanged; only **cross-unit linking** changes.

### `import` vs `use`

| Keyword | Where | Meaning |
|---------|--------|---------|
| **`import`** | **Module header only** (see below) | Pull symbols of a given **kind** from another **namespace** |
| **`use`** | Inside `connect`, diagram, extensions, transforms | Bind palette / presentation / provider / preset (`use palette lus_apps`, `use heatmap_tall`) |

No `using` directive. No `using var`. Parser: `import` before the module skeleton → import directive; `import` inside `card` / `report` → error.

### Module header

Immediately after `@tab` / `@dashboard` id and before `runtime` (strict order):

```text
@tab stakeholder

namespace Lus.Stakeholder

import diagrams from Lus.Stakeholder.Diagrams as stk
import layouts from Lus.Stakeholder.Layouts
import flows from Lus.Report as report_flow

runtime { … }
configuration { … }
connect { … }
report { … }
```

- `namespace` — optional once; if omitted, lint **warn** (path-derived namespace deprecated).
- `import` — zero or more consecutive lines after `namespace` (if any).
- Then [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) skeleton only.

### `import` — by kind

Reads **what → from where → optional short name** (declarative / SQL-like, not C# `using Alias = …`):

```text
import <kind> from <namespace> [as <alias>]
```

| Part | Meaning |
|------|---------|
| `<kind>` | Unit family to import (registry below). Enables **mixed units in one file**: consumer imports only `diagrams`, not layouts/types in the same namespace. |
| `<namespace>` | Dotted logical name or stdlib root (`Std.Charts`). |
| `as <alias>` | Optional qualifier for references (`stk.activity_5min`). If omitted, default qualifier = **last segment** of `<namespace>` (e.g. `Diagrams.activity_5min`). |

**Stdlib** (replaces `!include "<presentation/…>"`):

```text
import presentations from Std.Charts as pres
```

### Kind registry (`<kind>`)

Extensible registry (like `FlowGraphKindRegistry`). v1:

| `<kind>` | Unit roots | Symbols in table |
|----------|------------|------------------|
| `diagrams` | `@diagram` | diagram ids |
| `layouts` | `@layout` | layout ids |
| `presentations` | `@presentation` | presentation ids |
| `tooltips` | `@tooltip` | tooltip ids |
| `types` | `@types` / `type` | row type ids |
| `flows` | `@flow` | flow module + **inner** `source`/`transformer` node ids for data-flow LHS |
| `palettes` | `@palette` | palette ids |

v1: all units of that kind in the namespace are imported; `export` / visibility — follow-up ADR.

Duplicate ids after link → **error**.

### Referencing

```text
diagram ref stk.activity_5min
```

Data flow (LHS = dashflow node; RHS = `card.<id>.<slot>` per [ADR-0094](DASHSPEC-ADR-0094-report-page-data-flow.md)):

```text
import flows from Lus.Report as flow

data flow
  flow.Activity5min -> card.activity_5min.rows
end data flow
```

Diagram `import` does **not** imply data nodes; need `flows` import for producer ids.

### Mixed compilation units

A single file may declare `namespace X` and contain several `@diagram`, `@layout`, `@types`, … blocks. Consumers choose what they need:

```text
import diagrams from Lus.Stakeholder.Shared as d
import types from Lus.Stakeholder.Shared as t
```

Only the requested kind enters that consumer’s symbol table.

### Removal of `!include`

**`!include`**, glob includes, and file-level `.dashinclude` bundles are **removed** once the import resolver ships (no long-term dual link model). Migration: codemod to `namespace` + `import`; stdlib paths → `import … from Std.…`.

Fragment-level `include presentation "…"` inside `.dashdiagram` becomes either same-unit ref or `import` in that unit’s header (follow-up grammar detail).

### Compile pipeline integration

Before `compileScopeFlows`:

1. **`ResolveImports`** — expand `import` into diagram/layout/flow registries (today’s `IncludeExpander` output).
2. **`DocumentCompilePipeline`** (unchanged).

Register phases in [ADR-0097](DASHSPEC-ADR-0097-document-compile-pipeline.md); parsers do not link ad hoc.

### Project root

Marker file (`dashspec.toml` / `dspec.json`): `root_namespace`, source roots. LSP and `dspec build` share it.

## Migration

| From | To |
|------|-----|
| `!include "diagrams/foo/*.dashdiagram"` | `namespace` on units + `import … diagrams from …` |
| `connect { flow "flows/x.dashflow" }` | `import flows from Lus.X as f` + `connect { use flow f }` (connect syntax follow-up) |
| Multiple extensions | Optional single extension; **not required** with namespaces |

## Non-goals (v1)

- `runtime` / secrets in DSL ([ADR-0019](DASHSPEC-ADR-0019-runtime-directive.md)).
- `.dashcatalog` / `.dashhost` as language modules.
- Wildcard `import diagrams from Lus.*`.
- Replacing `card.<id>.<slot>` with namespace paths on RHS of data flow.

## Open questions

1. **Resolved 2026-10-10** — `connect { use flow <symbol> }`: binds an imported flow into the module shell; joins the `use <kind> <symbol>` family (`use provider <id>`, `use palette <id>`, `use <preset>`). Requires `as <alias>` or a full namespace name — no implicit single-flow binding. Legacy `connect { flow "path" }` is removed together with `!include`.
2. Rename `.dashspec` → `.dspec` when `dspec build` exists.

## Summary

- **`namespace`** — logical name for units in a compilation unit.
- **`import <kind> from <Namespace> [as alias]`** — kind-filtered link; **`use`** stays for in-block binds.
- **`!include` retired**; compiler front = **ResolveImports** → **DocumentCompilePipeline**.
