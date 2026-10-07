# DASHSPEC-ADR-0097: Document compile pipeline (phase registry)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-07 |
| **Relates to** | [ADR-0095](DASHSPEC-ADR-0095-dashboard-shell-wiring-pipeline.md), [ADR-0096](DASHSPEC-ADR-0096-placement-flow.md) |

## Decision

`DocumentCompilePipeline` is the **single orchestrator** for post-parse compile:

| Registry | Phases | Target |
|----------|--------|--------|
| **`ScopeFlows`** | data → placement → route | `ReportCompileContext` |
| **Document** | materialize → wiring_graph → validate | `DashboardDocument` |

Applicators (`ReportScopeDataFlowApplicator`, …), `DocumentFlowMaterializer`, `DocumentWiringGraphBuilder`, and `DashboardValidator` stay **separate modules**; new behavior **registers a phase**, parsers do not call applicators directly.

### Public API

- `compileScopeFlows` — after report body parsed; runs `ScopeFlows` registry.
- `attachWiringGraphAndValidate` — document assembled from `ReportCompileContext`, dashflow not materialized yet.
- `completeDocument` — full document registry (used by `DashboardComposer` and standalone `@tab`).

`ReportScopeFlowPipeline` remains a thin alias for ADR-0095 references.

## Terminology (ADR-0097)

- **`ReportCompileContext`** — mutable parse-time state for `report` / `@tab` body (replaces legacy name *dashboard shell context*).
- **`compileScopeFlows`** — apply `ScopeFlows` registry (not a terminal *shell*).

## Non-goals

- Nested classes with implementations (F# modules + registries).
- Merging `IncludeExpander` into this pipeline (parse/include is a separate stage).
