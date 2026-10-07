# DASHSPEC-ADR-0089: Document modules — `using` / `import` instead of preprocessor `!include`

| | |
|---|---|
| **Status** | Accepted (Phase 1 in code; Phase 2 project membership specified) |
| **Date** | 2026-10-06 (amended 2026-10-07) |
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

Treat each canonical `@-root` file as a **compilation unit** with a defined **export surface**. A **project** defines the **membership universe** (which files exist in the authoring package). An **entry module** (`@dashboard` / `@tab` referenced from catalog) declares **what it consumes** primarily through **use sites** (`report`, `connect`); the linker parses only units needed for that consumption, within the universe.

Explicit envelope `using` / `import` edges remain for **attach** (types/flow/layout not inferable from the report, narrowing, and IDE/review), not as a second copy of project membership.

**Preprocessor glob merge is deprecated** in the module envelope (phased removal). **Glob belongs in the project file** (or federation project graph), same semantics as today’s membership expand: deterministic path set, no merge order.

### Vocabulary

| Term | Meaning |
|------|---------|
| **Compilation unit** | One file, one `@` root (`@dashboard`, `@tab`, `@diagram`, `@types`, `@flow`, `@layout`, …) per [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md) |
| **Module** (document) | `@dashboard` / `@tab` envelope + its direct `using` edges + transitive units |
| **Export** | Named symbols made visible to importers (see table below) |
| **`using`** | Preferred keyword: bring exports from a unit into importers’ scope (or qualified) |
| **`import`** | Accepted alias for `using` at document envelope; **canonical path form** `import "logical/or/relative/path"` |
| **Project membership** | Set of compilation-unit paths (and optional globs) for one authoring package — slnx / `Compile Include` mental model |
| **Universe** | Expanded membership: all paths after glob expand + `.dashinclude` expand; **exclude** subtracts paths |
| **Consumption** | Symbols and unit paths an entry module actually needs — derived from use sites + `connect` + explicit attach edges |

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
| `.dashinclude` bundle | `.dashinclude` | **Deprecated** — migrate to `files` in `dashproject` or delete ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) follow-up) |

Stdlib paths `import "<presentation/heatmap_tall>"` ([ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md)) resolve to built-in units with the same export rules.

### Project membership (Phase 2)

Membership is **inventory**, not report DSL. It does **not** mean “paste file contents into the dashboard”. Operations on the set:

| Operation | Meaning |
|-----------|---------|
| **Path** | One compilation-unit file (extension optional; same rules as `SpecFragmentPaths`) |
| **Glob** | Expand to paths under a directory; `*` one level; lex sort for deterministic CI diff |
| **Exclude** | Remove paths from the current set (glob or explicit path); enables drafts in-tree without a second folder |

**Canonical surface (v1):** sidecar `*.dashproject` next to catalog / entry spec, or equivalent **federation** `AuthoringProject` graph ([ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) `DashSpecProject`).

```text
dashproject demo_soak
  root = "samples/demo"

  files
    diagrams/*.dashdiagram
    demo-rows.dashtype
    flows/demo-report.dashflow
    exclude diagrams/_scratch/**
  end files
end dashproject
```

- **`files` / `end files`** — positive entries only (paths or globs), one per line; no `include` verb required.
- **`exclude`** — optional; applied after all positive entries are expanded (MSBuild `Remove` style).
- **`.dashinclude`** — legacy positive bundle; migrates to `files` lines or `.dashmod` re-export index (optional).

Phase 1 may still list membership on the envelope (`!include` / `using "glob"`); Phase 2 lint **errors** on glob in envelope and points authors at `dashproject` / federation project.

### How the consumer declares consumption

The **consumer** is the entry `@dashboard` or `@tab` module (the `.dashspec` catalog points at). It does **not** re-declare the whole file list if project membership already defines the universe.

| Channel | Declares | Linker behavior |
|---------|----------|-----------------|
| **`report` body** | Diagram preset ids (`diagram <id>`), row shapes (`rows <Type>` / `input rows from <node>.<port>` scan) | Resolve ids against exports from paths in **universe**; parse only **referenced** `.dashdiagram` units when `LinkOnlyReferencedDiagramUnits` (default) |
| **`connect`** | `flow "…"`, `use palette …`, layout boards | Load linked `@flow` / layout / palette paths; flow unit may have its own internal edges |
| **Explicit attach** | `using "demo-rows.dashtype"`, `using flow "…"`, `using diagram <id> from "…"` | Required when multiple type files exist and report does not disambiguate; optional for documentation; validates id ↔ path for selective diagram attach |
| **Catalog / tab ref** | `tab … dashspec "other.dashspec"` | Child tab module has **its own** consumption closure; parent does not inherit child’s diagram registry ([open: shared project only]) |

**Default rule:** consumption = **transitive closure of use sites** ∩ **project universe**. Unused files in membership are **not parsed** for that entry (broken draft in folder does not fail soak if excluded or unreferenced).

Explicit per-diagram `using` lines are **not** required when membership + reference scan suffice (demo-soak target shape).

### Namespaces and qualifiers

**Author-facing symbol spaces are flat**, not hierarchical namespaces:

- Export id = `@diagram <id>`, `type <Name>`, `@flow <id>`, etc. — **global within the entry module’s resolved closure**.
- **No** `namespace foo { }` block in v1/v2 for DashSpec symbols.
- **Project membership is not a namespace** — it only bounds which files may contribute exports.

**Disambiguation when two units export the same id:** **Error** (`DS-USE003`) with both paths; fix by renaming an export or splitting projects. Optional **qualified use** at reference sites (v1.1+), not a separate namespace system:

```text
diagram ref heatmap stakeholder.peak_apps_heatmap
```

Qualifier prefix is an **import alias or module label** (e.g. tab id / bundle name), not a nested scope declaration. Defer `using stakeholder as s` syntax until a real clash forces it.

**Guiders `LogicalPath`** resolves **physical / workspace location** for federation `import`; it is **not** a DashSpec author namespace. Do not conflate with `diagram` preset ids.

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
  # diagrams: consumed via report references; membership in demo_soak.dashproject (Phase 2)

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

**Glob in envelope (Phase 1 compat)** — same expand/link semantics as project `files`; **Phase 2:** move globs to `dashproject` / federation; envelope glob → `DS-USE001`. `LinkOnlyReferencedDiagramUnits` (default runtime) parses only report-referenced `.dashdiagram` files from the universe; types/flow/layout units required by `connect` / explicit `using` are still loaded.

### Name resolution

| Use site | Rule |
|----------|------|
| `diagram … <id>` (in report) | `<id>` exported from a `.dashdiagram` in **universe**; defining unit parsed if referenced (or explicitly attached) |
| `rows <TypeName>` / row scan | `<TypeName>` from a `.dashtype` in universe (attach types file if ambiguous) |
| `connect { flow "path" }` | Links `@flow` at path (must be in universe or stdlib) |
| Duplicate export id in closure | **Error** with both defining paths |
| Missing export | **Error**: `DS-USE002: unknown diagram 'foo'` (fix report, membership, or attach) |

Resolution is **lazy**: parse only units needed for **consumption** ∩ **universe**; membership alone does not parse every file.

### Compiler pipeline (target)

Replace eager `IncludeExpander.expand` registry merge with:

```text
LoadProject(dashproject | federation) → Universe (paths ± exclude)
ParseEnvelope(text) → EnvelopeAst (attach edges + connect + report body)
ScanConsumption(report, connect) → referenced diagram ids, row types, flow paths
SelectUnits(universe, consumption, attach) → unit paths to parse
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

- **`*.dashproject`** (or federation project): owns `files` / `exclude` membership; catalog references project + entry `.dashspec`.
- Lint **error** on envelope `!include` / glob (`DS-USE001`); migrate to project `files`.
- `.dashinclude` → `files` lines or optional `.dashmod` index.
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

1. **`.dashmod` bundle** — optional re-export index inside universe vs only `files` + consumption scan?
2. **Tab modules** — shared `dashproject` for catalog tree vs per-tab project; child tab closure independent (current decision) but shared universe default?
3. **LogicalPath** — exact mapping to Guiders `import` (single resolver package shared with other DSLs); distinct from flat diagram/type ids.
4. **Row-type attach** — auto-load sole `.dashtype` in universe vs always require `using types "…"` when multiple type files exist.

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
