# DASHSPEC-ADR-0095: Dashboard shell wiring pipeline (SSOT)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-07 |
| **Relates to** | [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md), [ADR-0093](DASHSPEC-ADR-0093-qualified-flow-graph-kinds.md), [ADR-0094](DASHSPEC-ADR-0094-report-page-data-flow.md) |

## Problem

Qualified scope flows (`data` / `show` / `wire` / `action`) were applied by calling separate applicators from `DocumentModuleParser` and `TabModuleParser`. That duplicated order, was easy to forget on new entry points, and forced card parse-time validation before report `data flow` could wire slots (workarounds in slot builder).

## Decision

### SSOT module

**`DocumentCompilePipeline.ScopeFlows`** (alias `ReportScopeFlowPipeline`) is the **only** place that defines **post-parse scope flow order** on `ReportCompileContext`:

1. **`ScopeDataFlow`** — report/page `data flow` → `card.<id>.<slot>` (and validation that every slot is wired).
2. **`ScopePlacementFlow`** — `placement flow` → report / page / card toolbars.
3. **`ScopeRouteFlow`** — show / wire / action → chrome, host, events.

Entry points call **`DocumentCompilePipeline.compileScopeFlows`** once after the report body is parsed — same lifecycle idea as `IncludeExpander.linkEnvelope` for includes (one orchestrator, not scattered calls). See [ADR-0097](DASHSPEC-ADR-0097-document-compile-pipeline.md).

### Parse vs wire

| Stage | Responsibility |
|-------|----------------|
| Token parse (`CardParser`, `parseReportBlock`, …) | Collect AST; card slots may use **deferred** data placeholders until wiring runs. |
| `IncludeExpander` / `connect` | Register external units (diagram, dashflow, layout, …). |
| **`DocumentCompilePipeline.compileScopeFlows`** | Resolve all scope **flow** sections onto cards. |
| `DocumentFlowMaterializer.materialize` | Resolve dashflow node → `DataSource` on cards (document level). |
| `DocumentWiringGraphBuilder.build` | Export unified graph for Studio. |

New applicators for scope flows **register a phase** in `DocumentCompilePipeline.ScopeFlows.definitions`; do not call them from parsers.

### Relation to `FlowGraphKindRegistry`

- **Registry** = author-facing qualified block keywords (`data flow`, …).
- **Pipeline** = runtime order those partitions are applied to `ReportCompileContext`.

## Non-goals (v1)

- Single-pass parse interleaving cards and report `data flow` (still two-phase: parse shell, then wire).
- Merging `IncludeExpander` and wiring pipeline into one type (same pattern, separate concerns).
