# DASHSPEC-ADR-0090: Card interior flow (inputs → slots)

| | |
|---|---|
| **Status** | Accepted (parser v1) |
| **Date** | 2026-10-06 |
| **Relates to** | [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0088](DASHSPEC-ADR-0088-flow-composition-subprocess.md), [ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md) |

## Context

Module `.dashflow` holds shared extract/transform (`source`, `transformer`, `->` links). Multi-slot cards need **interior wiring**: which module port feeds which diagram slot, and which dashboard filters apply per slot — without merging card layout into the module graph.

## Decision

### Planes

| Layer | Owns |
|-------|------|
| Module dashflow | Shared sources, transformers, `producer [out] -> [in] consumer` |
| Card `input <alias> from node.port` | Boundary: alias → module output port (not a graph node) |
| Card `flow … end flow` | Interior only: links **input alias → slot id**; `bind <slot> …` per slot |

Interior flow **does not** add nodes to `DashboardDocument.Dashflow`. Parse resolves links/binds onto `CardDiagramSlot` (`FlowInput`, `BoundFilters`) for the existing binder/runtime.

### Grammar (v1)

```text
input heatmap from daily_peak_concurrent_proxy_heatmap_tz.rows
input drill from daily_peak_concurrent_proxy_at_peak_tz.rows

flow
  heatmap -> heatmap
  drill -> drill
  bind heatmap usage_date, app_name
  bind drill usage_date, app_name
end flow
```

- Links reuse [FlowLinkParser](src/DashSpec.Modeling.Parse/DataFlow/FlowLinkParser.fs) (`->` = producer to consumer).
- `bind <slotRef>` accepts comma list or block (same filter names as `data for … bind`).
- Legacy `input for <slot> from …` and `data for <slot> bind …` remain valid.

### Non-goals (v1)

- Transform/filter **nodes** inside card flow (multi-hop `input -> filters -> slot`).
- Merging interior flow into module `FlowGraph` IR.

## Follow-up

- Optional `apply` / filter nodes as interior transformers (same link syntax).
- Core/Studio round-trip of `CardInputs` + `InteriorFlow` on `CardDefinition`.
