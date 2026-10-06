# DASHSPEC-ADR-0089: Document modules — `using` / `import` instead of preprocessor `!include`

| | |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-10-06 |
| **Relates to** | [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md), [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md), [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md), [ADR-0088](DASHSPEC-ADR-0088-flow-composition-subprocess.md) |

## Context

### What we have today

Module envelope parsing ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) Layer 2) treats file attachment as **preprocessor expansion**:

- `!include "path"` or `import "path"` (same hook: `TryModuleInclude` in the parser) resolves a path or **glob**, reads each matching file, and **merges** into side-effect registries (`ModuleIncludeState`: diagrams, layouts, row types, tooltips, dashflow).
- Glob order is **lexicographic by full path** (deterministic IR, not execution order).
- Cards consume presets via **`diagram ref … <id>`** and typed rows via **`rows <TypeName>`** — symbolic references, but **no explicit import edge** from the dashboard module to the defining compilation unit.
- **Every** file matched by glob is parsed **eagerly** during envelope parse; an unused or broken fragment fails the whole `@dashboard` / `@tab` module.

This matches PlantUML-style `!include` ([ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md)) and the hybrid authoring profile (`!include "diagrams/<tab>/*.dashdiagram"`). It is **not** the compilation-unit model familiar from mainstream languages (separate modules, exported symbols, dependency closure, unused units ignored).

### Pain

| Symptom | Root cause |
|---------|------------|
| One bad `.dashdiagram` in a folder glob breaks soak | Eager expand of all glob matches |
| “Registry” vs “usage graph” confusion | Includes build a dictionary; `connect` / `@flow` wire data separately |
| Duplicate `@diagram` id across files | Flat merge after glob |
| Hard to reason about “what this module depends on” | No `using` list; only implicit glob |
| `import "path"` feels like a keyword but behaves like `#include` | No symbol exports |

### Platform direction

Guiders platform work targets a unified **`import`** with **logical paths** (workspace / project roots), deprecating document-level `!include` for new authoring. DashSpec should align envelope linking with that model while keeping **fragment** `@-file` roots from [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md).

### Not in scope for this ADR

- **`extensions { import … from "…dll" }`** — plugin assembly load ([ADR-0032](DASHSPEC-ADR-0032-extension-blocks-and-plugins.md), [ADR-0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md)); different `import` namespace.
- **Runtime data plane** — `connect { flow "…" }`, `@flow`, nested `flow` ([ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0088](DASHSPEC-ADR-0088-flow-composition-subprocess.md)); unchanged except that flow/type units are linked via the same module graph.
- **`include_once` / zip archives** — remain non-goals from [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md).

## Decision

### Principle

Treat each canonical `@-root` file as a **compilation unit** with a defined **export surface**. A dashboard/tab module declares **dependencies** with `using` / `import`; the compiler builds a **dependency DAG**, parses only the **transitive closure**, and resolves symbols through that graph.

**Preprocessor glob merge is deprecated** for module envelope (phased removal). Tooling may still offer “add all diagrams in folder to project” as a **codemod / IDE action**, not language semantics.

### Vocabulary

| Term | Meaning |
|------|---------|
| **Compilation unit** | One file, one `@` root (`@dashboard`, `@tab`, `@diagram`, `@types`, `@flow`, `@layout`, …) per [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md) |
| **Module** (document) | `@dashboard` / `@tab` envelope + its direct `using` edges + transitive units |
| **Export** | Named symbols made visible to importers (see table below) |
| **`using`** | Preferred keyword: bring exports from a unit into importers’ scope (or qualified) |
| **`import`** | Accepted alias for `using` at document envelope; **canonical path form** `import "logical/or/relative/path"` |

Fragment-level `include diagram` inside `.dashdiagram` remains **intra-fragment composition** until a follow-up unifies fragment includes with the same linker (Phase 3).

### Export surface by unit kind

| Unit root | File extension | Exported symbols (v1) |
|-----------|----------------|------------------------|
| `@diagram <id>` | `.dashdiagram` | Diagram preset id = `<id>` |
| `@types …` / `type` blocks | `.dashtype` | Each `type <Name>` |
| `@flow <id>` | `.dashflow` | Flow graph id = `<id>` (for `connect { flow … }` / cross-ref) |
| `@layout <id>` | `.dashlayout` | Layout board id = `<id>` |
| `@presentation <id>` | `.dashpresentation` | Presentation preset id |
| `@tooltip <id>` | `.dashtooltip` | Tooltip id |
| `.dashinclude` bundle | `.dashinclude` | **Deprecated** — migrate to explicit `using` list or delete ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) follow-up) |

Stdlib paths `import "<presentation/heatmap_tall>"` ([ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md)) resolve to built-in units with the same export rules.

### Envelope syntax (target)

`using` / `import` appear in the module envelope **after** `configuration`, **before** `connect` and `report` (order preserved for readability; linker order is DAG, not source order).

```text
@dashboard demo_soak
  runtime
    manifest = "demo.toml"
  end runtime

  configuration
    sqldialect = tsql
    palette = "palettes/demo-apps.dashpalette"
  end configuration

  using types "demo-rows.dashtype"
  using diagram demo_activity_5min_line from "diagrams/activity-5min-line.dashdiagram"
  using diagram demo_peak_apps_heatmap from "diagrams/peak-apps-heatmap.dashdiagram"
  # … explicit per preset, or grouped bundle file (future: using diagrams "stakeholder/index.dashmod")

  connect
    use palette demo_apps
    flow "flows/demo.dashflow"
    layout grid
      columns = 12
      gap = 16
    end grid
  end connect

  report
    …
  end report
end dashboard
```

**Forms (normative target):**

1. **Path import (re-export all exports of unit)** — `using "demo-rows.dashtype"` or `import "diagrams/peak-apps-heatmap.dashdiagram"`.
2. **Selective import** — `using diagram <id> from "relative/path.dashdiagram"` (id must match `@diagram` in file; linker validates).
3. **Qualified use (optional v1.1)** — `diagram ref heatmap stakeholder.peak_apps_heatmap` when multiple imports could clash; if omitted, unqualified id must be unique in the dependency closure.

**Glob in envelope — rejected** for the target language. Lint: `DSxxxx: glob in using/import is not allowed; list units or use tooling`.

### Name resolution

| Use site | Rule |
|----------|------|
| `diagram ref … <id>` | `<id>` must be exported by a unit in the **dependency closure** of this module |
| `rows <TypeName>` | `<TypeName>` exported from a `.dashtype` unit in closure |
| `connect { flow "path" }` | Parses/links `@flow` unit at path; transitive `using` inside flow file applies to that unit |
| Duplicate export id in closure | **Error** with both defining paths |
| Missing export | **Error**: `DSxxxx: unknown diagram 'foo'; add using …` |

Resolution is **lazy per closure**: units not reachable from `using` / `import` are not parsed for this module.

### Compiler pipeline (target)

Replace eager `IncludeExpander.expand` registry merge with:

```text
ParseEnvelope(text) → EnvelopeAst (using edges + connect + report body)
BuildDependencyGraph(envelope) → DAG of compilation units
TopologicalParse(units) → per-unit ParseDocument (existing @-root parsers)
LinkSymbols(module, units) → ResolvedModuleDiagrams, ResolvedRowTypes, …
ParseReportBody(report, linked scope)
```

`DashboardComposer` and Core `DashboardDocument` keep the same **resolved dictionaries** at the boundary ([ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md)); only **how** those dictionaries are populated changes.

### Relationship to data plane

| Layer | Linking mechanism |
|-------|-------------------|
| Diagram / layout / types / tooltips | **Document modules** (`using`) — this ADR |
| Sources, transformers, wires | **`@flow`** graph inside linked flow unit ([ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md)) |
| Card inputs | Ports wired in flow + card `input` / legacy `datasource` migration |

No change: `using` does **not** replace `connect` or flow edges.

## Migration

### Phase 0 — Documentation + lint (no breaking parse)

- Accept ADR; document `import "path"` as **legacy alias** of `!include` (current behavior).
- Analyzer warning on glob `!include` / `import` with `*`.
- Authoring guide: prefer explicit paths; codemod recipe `glob → N using lines`.

### Phase 1 — Dual mode

- Parser accepts **both** legacy `!include` / glob and new `using` / selective `using … from`.
- Legacy path: unchanged eager expand (compat).
- New path: build closure only from `using`; **do not** parse unlisted units.
- Tests: same IR for equivalent explicit `using` list vs legacy glob on green samples.

### Phase 2 — Default new semantics

- New files: lint **error** on `!include` and glob (fix: `using`).
- `.dashinclude` registry bundles: deprecate; migrate to `using` list or single “index” module file (format TBD).
- Fragment `!include` inside `.dashdiagram` → `using presentation "…"` with same linker (subset).

### Phase 3 — Remove preprocessor

- Remove `!include` and envelope glob from grammar.
- `import` at envelope = only module `import` / `using` (align Guiders `import` / LogicalPath where integrated).

## Consequences

### Positive

- Failures isolated to **imported** units (fixes “unused broken file in glob” class).
- Explicit **dependency list** in spec (review, CI, Studio “dependencies” panel).
- Same mental model as mainstream compilers; easier onboarding.
- Aligns with platform `import` / logical paths.

### Negative / cost

- Hybrid demo/demo specs must **enumerate** diagrams (or use codemod); more lines than one glob.
- Parser/linker implementation: DAG, cycle detection, duplicate diagnostics.
- IDE: go-to-definition, find references, quick-fix “add using”.

### Analyzer rules (initial set)

| Code | Severity | Condition |
|------|----------|-----------|
| `DS-USE001` | Warning → Error | Glob in `!include` / `import` |
| `DS-USE002` | Error | Unknown symbol at `diagram ref` / `rows` |
| `DS-USE003` | Error | Duplicate export in closure |
| `DS-USE004` | Error | Cycle in `using` graph |
| `DS-USE005` | Warning | `!include` deprecated |

## Open questions

1. **Bundle file** — `.dashmod` / re-export list (`export diagram a, b from "./a.dashdiagram"`) vs pure explicit `using` lines only in v1?
2. **Qualified names** — mandatory when closure has clashes, or always allow optional qualifier?
3. **Tab modules** — same envelope rules as `@dashboard`; merged tab dashspec refs inherit parent closure or declare own `using`?
4. **LogicalPath** — exact mapping to Guiders `import` (single resolver package shared with other DSLs).

## Implementation notes (dash-spec repo)

| Area | Change |
|------|--------|
| `IncludeExpander.fs` | Split: legacy expander vs `ModuleLinker.fs` |
| `DocumentModuleParser.fs` | Parse `using` directives into `EnvelopeAst` |
| `ModuleIncludeState.fs` | Populate from linker, not eager glob |
| Analyzers | `DS-USE*` rules |
| Samples | `demo-soak`, demo tabs: explicit `using` after Phase 1 |
| Tests | Closure-only parse; unknown symbol; duplicate id; no parse of unimported broken file |

## Supersedes / amends

- **Amends** [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) Layer 2: glob and registry-first includes are **legacy**; target is `using` + linker.
- **Amends** [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md): module envelope linking; fragment `!include` unchanged until Phase 2–3.
