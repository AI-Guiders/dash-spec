---
type: ADR
id: DASHSPEC-ADR-0101
title: "Single compilation-unit file type (`.ds`) and `module <kind> <id>` envelope"
status: "Accepted"
date: 2026-10-11
tags: [dashspec, adr]
language: ru
relates:
  - DASHSPEC-ADR-0017
  - DASHSPEC-ADR-0024
  - DASHSPEC-ADR-0089
  - DASHSPEC-ADR-0097
  - DASHSPEC-ADR-0098
---

# DASHSPEC-ADR-0101: Single compilation-unit file type (`.ds`) and `module <kind> <id>` envelope

## Context

The compilation-unit model (ADR-0089) says "one file, one `@` root". In practice the unit zoo spread across **ten file extensions** (`.dashspec`, `.dashdiagram`, `.dashlayout`, `.dashpresentation`, `.dashtooltip`, `.dashpalette`, `.dashtype`, `.dashflow`, `.dashcatalog`, `.dashhost`) produced recurring friction:

- **Discovery cost** — a referenced preset can exist in a sibling package and simply be missing from the local folder; nobody can tell from the file name which zoo member it belongs to.
- **Glob plumbing** — project indexes carry `kind` + `glob` pairs that mostly mirror the extension table; extension-less paths are normalized by force-appending one of ten suffixes.
- **Envelope sigil** — the `@` root (`@diagram x`) exists only to separate the module envelope from body keywords (`diagram`, `layout`, `flow`, `type`, `filter` are body statements).

## Decision

### 1. One canonical unit file type: `.ds`

Every compilation unit — report, tab, diagram preset, layout, presentation, tooltip, palette, row types, flow, catalog, host — is a `.ds` file. The unit **kind comes from the module root**, not from the file name.

Legacy extensions remain **readable** during migration (compat), but tooling, samples, and new authoring use `.ds`. Unit classification is **content-first**: the `@`-root / `module` root of the file decides the kind; the extension is only a legacy fallback (`UnitFileClassifier`).

### 2. Envelope syntax: `module <kind> <id> … end <kind>`

```text
module diagram demo_peak_heatmap
  ...
end diagram
```

- Replaces `@diagram demo_peak_heatmap`. The `@`-root form is **removed from the language**; all in-repo specs and test fixtures are rewritten mechanically.
- The closing marker stays `end <kind>` (block symmetry unchanged).
- The marker is required: the envelope kind must be unambiguous against body keywords.
- **File directives** (`@runtime`, `@config`, `@sqldialect`, `@diagramlibrary`, `@palette "<path>"`) keep the `@` sigil for now — they collide with body keywords and migrate as a follow-up (likely a `file { … }` block or module-line arguments).

### 3. Kind set (v1)

`dashboard`, `tab`, `diagram`, `layout`, `presentation`, `tooltip`, `palette`, `types`, `flow`, `catalog`, `host`, `include` (deprecated bundle).

### 3. Project file: `.dsproj`

The project file is the **source of truth** for a package: `root_namespace`, `source_roots`, legacy switches, and the `files` / `exclude` membership (modules are found by their `namespace` header) ("where the modules are"). Imports and path resolution read the project — no text heuristics, no implicit roots.

- Canonical name: **`*.dsproj`** (one per package directory; the parser's resolution index reads `*.dsproj` only — the legacy `dashspec.toml` marker with `[[index]]` tables belongs to the deferred platform authoring layer, not to the compiler).
- Membership globs live here (per ADR-0089 Phase 2); `exclude` follows in the membership extension.
- Missing-import diagnostics resolve against project membership and carry deterministic positions (the import directive carries its source offset).

## Consequences

- One extension to discover, glob, watch, and associate with editors: `*.ds`.
- `dashspec.toml` has no index tables; modules are found by their `namespace` header under the source roots. `kind` filters the import, not a filename convention.
- Board references are unit references: a board is an imported layout unit (`import layouts from <NS>`, unit with `scope tab|toolbar|host`) or an inline `layout … end layout` inside a card. The removed `include layout "…"` / `include toolbar "…"` path forms have no replacement in the language.
- Mechanical migration: root rewrite (`@x` → `module x`) + file rename; no grammar change inside bodies.
- Follow-ups: file-directive sigil removal; LSP classification/coloring update; deprecation removal of legacy extensions.
