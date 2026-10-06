# DASHSPEC-ADR-0091: Unified Graph — one wire syntax

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-06 |
| **Relates to** | [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0088](DASHSPEC-ADR-0088-flow-composition-subprocess.md), [ADR-0090](DASHSPEC-ADR-0090-card-interior-flow.md), [ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md) |

## Context

DashSpec had multiple wiring notations (`input from`, `bind`, `data for bind`, dashflow links). That is **split brain** for authors and Studio.

## Decision

### Graph (IR)

```text
Graph
├── nodes (source, transformer, filter, slot, chrome, …)
├── edges: producer [out] -> [in] consumer
└── edgeKind: flow | route | event (inferred from port types)
```

**Flow** edges move `TypedRowBatch`. **Route** edges attach filter identity to slot query, chrome, or wire — not rowsets.

### One authoring syntax for all wiring

**Only link lines** wherever connections are declared:

```text
producer [outPort] -> [inPort] consumer
```

| Scope | Container | Example |
|-------|-----------|---------|
| Module | `@flow` / `connect { flow … }` | `raw [raw] -> [raw] peak_tz` |
| Card | `card … flow … end flow` | [ADR-0090](DASHSPEC-ADR-0090-card-interior-flow.md) |
| Report / page | `report` / `page` `flow` | See [ADR-0092](DASHSPEC-ADR-0092-report-scope-routing-and-events.md) |

**Not** a second notation: no `bind` lists for wiring, no `input … from` in new specs (legacy parse only).

Definitions (`filter`, `source`, `slot` + `ports`) are **blocks**; wiring is **always** links.

### Slot ports (authoring target)

```text
slot heatmap
  diagram …
  ports
    in rows
    in usage_date
    in app_name
  end ports
end slot
```

Link endpoints must match declared `in` ports when `ports` block is present.

### Legacy → links (migration)

| Legacy | Link form |
|--------|-----------|
| `input heatmap from node.rows` | `node [rows] -> [rows] heatmap` |
| `bind heatmap f1, f2` | `f1 -> [f1] heatmap`, `f2 -> [f2] heatmap` |
| `filters { f1 }` on card (chrome) | unchanged until report route links land |
| `on click set X from y` | event edge (later) or keep `on click` block |

### Studio

One matrix / canvas: same edge shape; filter views by `edgeKind`.

## Phasing

| Phase | Deliverable |
|-------|-------------|
| **P1** (current) | Card `flow` = links only; flow/route inference; legacy `input` parse |
| **P2** | `slot` + `ports`; Graph IR export |
| **P3** | [ADR-0092](DASHSPEC-ADR-0092-report-scope-routing-and-events.md): report/page chrome, host, derive, events |
| **P4** | Rename module IR `FlowGraph` → `Graph` subgraph |

## Non-goals

- Multiple equivalent wire notations in new authoring.
- Route edges that run SQL or row transforms.
