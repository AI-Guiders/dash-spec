# DASHSPEC-ADR-0098: Compiler modules — `namespace` and `using`

| | |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-10-07 |
| **Relates to** | [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md), [ADR-0097](DASHSPEC-ADR-0097-document-compile-pipeline.md) |

## Context

DashSpec today is a **multi-root DSL** (many file extensions, `!include`, globs) linked at parse time ([ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md)). [ADR-0097](DASHSPEC-ADR-0097-document-compile-pipeline.md) introduced a **document compile pipeline** (scope flows → materialize → graph → validate) but not a single **module graph** or symbol table.

Authors (.NET-heavy teams) want **namespaces** and **`using`** instead of path strings and extension zoo. Goal: a **compiler** with a clear front-end (resolve imports) and the existing pipeline as middle-end.

## Decision (v1 — language; implementation phased)

### Compiler shape

| Stage | Responsibility |
|-------|----------------|
| **Front** | Parse units → build **symbol table** (`namespace` + `using`) → **link** legacy `!include` / paths during transition |
| **Middle** | `DocumentCompilePipeline` (unchanged contract): `compileScopeFlows` → document phases |
| **Back** | `DashboardDocument`, `WiringGraph`, diagnostics, optional emitted bundle (Host cache) |

Entry: `@dashboard` / `@tab` module (today `.dashspec`; future `.dspec` is a rename only).

### Namespace declaration

Every compilable unit may declare a logical namespace (file-level or module-level):

```text
namespace Lus.Stakeholder.Diagrams

@diagram activity_5min
  heatmap { … }
end diagram
```

**Rules (v1):**

- Namespace segments: dotted identifiers (`A.B.C`), case-sensitive compare with optional case-insensitive lookup for refs (implementation: OrdinalIgnoreCase for ids, preserve author casing in diagnostics).
- Default: if omitted, derive from **project root** + relative path (deprecated; lint **warn**). Explicit `namespace` required for new files.
- Unit **id** (`@diagram activity_5min`) is unique within its namespace; fully qualified name (FQN) = `Lus.Stakeholder.Diagrams.activity_5min`.

Physical files may keep current extensions during migration; namespace is orthogonal to path ([ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md)).

### `using` — import by kind

Keyword **`using`** (not `import`; familiar to C# authors). Form:

```text
using [<alias> =] <kind> from <namespace>
```

| Part | Meaning |
|------|---------|
| `<kind>` | Which **unit family** to import from the target namespace (see registry below). Plural keyword matches authoring vocabulary. |
| `<namespace>` | Dotted namespace or stdlib root (see stdlib). |
| `<alias>` | Optional; short **qualifier** for references. If omitted, default qualifier = last segment of `<namespace>` (e.g. `Diagrams.activity_5min`). |

**Examples:**

```text
using stk = diagrams from Lus.Stakeholder.Diagrams
using layouts from Lus.Stakeholder.Layouts
using report_flow = flows from Lus.Report
```

**Stdlib** (replaces angle-bracket paths over time):

```text
using pres = presentations from Std.Charts
```

Legacy `!include "<presentation/heatmap_tall>"` remains accepted until deprecation; resolver may lower to `using presentations from Std.…`.

### Kind registry (`<kind>`)

Extensible registry (like `FlowGraphKindRegistry`). v1 kinds:

| `<kind>` | Unit roots (ADR-0017) | Imported symbols |
|----------|----------------------|------------------|
| `diagrams` | `@diagram` | diagram ids |
| `layouts` | `@layout` | layout ids |
| `presentations` | `@presentation` | presentation ids |
| `tooltips` | `@tooltip` | tooltip ids |
| `types` | `@types` / `type` | row type ids |
| `flows` | `@flow` | flow module id + internal node ids (separate rule for `source`/`transformer`) |
| `palettes` | `@palette` | palette ids |

`using` imports **all** public units of that kind exported from the namespace (v1: all units in namespace; `export` / visibility — follow-up ADR).

Duplicate ids after merge → **error** (same as post-glob expand [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md)).

### Referencing imported symbols

With alias `stk`:

```text
diagram ref stk.activity_5min
```

In **qualified flows** (data / placement / wire / action), the same qualifier applies to **module graph nodes** and **card/slot anchors** where a symbol names a dashflow node or diagram-bound port:

```text
data flow
  stk.Activity5min -> card.activity_5min.rows
end data flow
```

Notes:

- Left-hand **module node** ids come from the imported `flows` unit (or from `connect { flow … }` until flows are namespaced). Diagram-only aliases do not create data nodes unless a `flows` import defines `Activity5min` as `source`/`transformer`.
- Right-hand **`card.<id>.<slot>`** stays as today ([ADR-0094](DASHSPEC-ADR-0094-report-page-data-flow.md)); only the **producer** side may use `alias.NodeId`.
- Identifier segments after `alias.` use the same rules as unqualified ids (case-insensitive match).

Placement example (unchanged anchors, qualifier only where needed):

```text
placement flow
  usage_date -> report
end placement flow
```

### Placement in module skeleton

`using` is allowed:

- At **module** scope (`@tab` / `@dashboard` body), after `configuration`, before `connect` / `report` (exact ordering TBD in grammar; lint enforces one block of consecutive `using` lines).
- Not inside `card { }` in v1 (card uses qualifiers from module scope).

Coexists with legacy:

```text
!include "diagrams/stakeholder/*.dashdiagram"   // deprecated, warning
using stk = diagrams from Lus.Stakeholder.Diagrams
```

Compiler **link phase** merges both; prefer `using` when the same id exists in two sources → **error**.

### Compile pipeline integration

New front-end phase (before `compileScopeFlows`):

1. **`ResolveUsings`** — expand `using` into `ModuleIncludeState` / diagram registry (same structures `IncludeExpander` populates today).
2. **`LinkLegacyIncludes`** — `!include`, globs, `.dashinclude` (until removed).
3. Existing **`DocumentCompilePipeline`**.

Register in a single **compile phase registry** ([ADR-0097](DASHSPEC-ADR-0097-document-compile-pipeline.md)); do not call resolvers from parsers ad hoc.

### Project root

Compiler needs a **project marker** (e.g. `dashspec.toml` or nearest `@dashboard` / `dspec.json`) defining:

- `root_namespace` (optional prefix, e.g. `Lus`)
- `src` paths for unit discovery

LSP / `dspec build` use the same root. Host file-watcher may trigger incremental recompile per changed FQN.

## Migration

| From | To |
|------|-----|
| `!include "diagrams/foo/*.dashdiagram"` | `namespace` per file + `using foo = diagrams from …` |
| `diagram ref heatmap` (local id) | `diagram ref stk.heatmap` when diagram lives in another namespace |
| `connect { flow "flows/x.dashflow" }` | `using f = flows from Lus.X` + `connect { use flow f }` (syntax follow-up) |
| Multiple extensions | Optional single `.dspec` extension; **not required** if namespace + `using` are present |

No big-bang: v1 implement resolver + diagnostics; LUS may adopt namespace/`using` tab-by-tab.

## Non-goals (v1)

- Merging `runtime` manifest / SQL connection strings into DSL (stay TOML, [ADR-0019](DASHSPEC-ADR-0019-runtime-directive.md)).
- `.dashcatalog` / `.dashhost` as language units (deployment metadata).
- `using static` / wildcard `using diagrams from Lus.*` (explicit namespace only).
- Replacing `card.<id>.<slot>` with namespace syntax in v1.

## Open questions

1. Default alias when omitted: last segment vs full namespace dotted as qualifier?
2. `flows` import: expose only module boundary id or all inner node ids in symbol table?
3. Rename `.dashspec` → `.dspec` in a separate ADR once `dspec build` exists.

## Summary

- **`namespace`** — logical module id for units.
- **`using [alias =] <kind> from <namespace>`** — kind-filtered import; **alias** prefixes references (`stk.activity_5min`, `stk.Activity5min` in flows).
- **Compiler front** links `using` + legacy includes → existing **DocumentCompilePipeline**.
