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
| **`ShellWiring`** | data → placement → route | `DashboardShellContext` |
| **Document** | materialize → wiring_graph → validate | `DashboardDocument` |

Applicators (`ReportScopeDataFlowApplicator`, …), `DocumentFlowMaterializer`, `DocumentWiringGraphBuilder`, and `DashboardValidator` stay **separate modules**; new behavior **registers a phase**, parsers do not call applicators directly.

### Public API

- `finalizeShell` — after report body parsed; runs `ShellWiring` registry.
- `attachWiringGraphAndValidate` — document built from shell, dashflow not materialized yet.
- `completeDocument` — full document registry (used by `DashboardComposer` and standalone `@tab`).

`DashboardShellWiringPipeline` remains a thin alias for ADR-0095 references.

## Non-goals

- Nested classes with implementations (F# modules + registries).
- Merging `IncludeExpander` into this pipeline (parse/include is a separate stage).
