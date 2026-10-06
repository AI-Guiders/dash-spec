# DASHSPEC-ADR-0090: Card wiring (links only)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-06 |
| **Relates to** | [ADR-0091](DASHSPEC-ADR-0091-unified-graph-flow-and-routing.md), [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md) |

## Context

Multi-slot cards need module row streams and per-slot filter participation without merging card layout into module dashflow. See [ADR-0091](DASHSPEC-ADR-0091-unified-graph-flow-and-routing.md) for the unified **Graph** model.

## Decision

### One wiring syntax on the card

Inside `card … flow … end flow`, **only link lines**:

```text
producer [outPort] -> [inPort] consumer
```

Parser classifies each link ([CardInteriorFlowResolver](../src/DashSpec.Modeling.Parse/Card/CardInteriorFlowResolver.fs)):

| Producer | Consumer | Kind |
|----------|----------|------|
| Module node id (`*_tz`) | Diagram slot id | **Flow** → `CardDiagramSlot.FlowInput` |
| Report `filter` id | Slot + `[filter]` port | **Route** → `BoundFilters` |
| Card `input` alias (legacy) | Slot | **Flow** (prefer module link) |

**Example** (multi-slot card):

```text
flow
  report_heatmap_tz [rows] -> [rows] heatmap
  report_drill_tz [rows] -> [rows] drill
  usage_date -> [usage_date] heatmap
  usage_date -> [usage_date] drill
  app_name -> [app_name] heatmap
  app_name -> [app_name] drill
  bucket_time -> [bucket_time] drill
end flow
```

`bind` inside `flow` is a parse error.

### Definitions stay blocks

- `filter …` — column, widget, label ([ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md)).
- `slot … ports …` — **target** grammar (not required for parse v1; ports validated when present).
- `diagram`, `views`, `filters { }` (chrome), `on click` — presentation / behaviour.

### Legacy (parse still accepts; do not use in new specs)

| Legacy | Replace with |
|--------|----------------|
| `input <slot> from node.port` | `node [port] -> [rows] <slot>` |
| `flow bind <slot> f1, f2` | `f1 -> [f1] <slot>`, `f2 -> [f2] <slot>` |
| `data for <slot> bind …` | route links in `flow` |
| `input for <slot> from …` | module flow link |

### Runtime

Parse resolves links onto `CardDiagramSlot` (`FlowInput`, `BoundFilters`); [DocumentFlowBinder](../src/DashSpec.Execution.Runtime/DocumentFlowBinder.cs) unchanged.

## Non-goals

- Merging card links into module `Dashflow` IR.
- Chrome/host route links in card `flow` (report scope; later).

## Follow-up

- `slot … ports in …` parse + lint (edge must target declared port).
- Report-level route links (`filter -> chrome card`).
