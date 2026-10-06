# DASHSPEC-ADR-0090: Card interior flow (inputs → slots)

| | |
|---|---|
| **Status** | Accepted (parser v1) |
| **Date** | 2026-10-06 |
| **Relates to** | [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0088](DASHSPEC-ADR-0088-flow-composition-subprocess.md), [ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md), [ADR-0091](DASHSPEC-ADR-0091-unified-graph-flow-and-routing.md) |

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
  bind heatmap usage_date, app_name
  bind drill usage_date, app_name
end flow
```

- When a card `input` alias matches a diagram slot id (`input heatmap` + slot `heatmap`), wiring is **implicit** (no `heatmap -> heatmap`).
- Explicit wiring when names differ: `facts -> heatmap` (same [FlowLinkParser](src/DashSpec.Modeling.Parse/DataFlow/FlowLinkParser.fs) arrow), or shorthand `slot heatmap` (= feed input `heatmap` into slot `heatmap`).
- `bind <slotRef>` accepts comma list or block (same filter names as `data for … bind`).
- Legacy `input for <slot> from …` and `data for <slot> bind …` remain valid.

### Non-goals (v1)

- Transform/filter **nodes** inside card flow (multi-hop `input -> filters -> slot`).
- Merging interior flow into module `FlowGraph` IR.

## Follow-up (v2): interior graph nodes + filter wiring

### Two graphs (do not merge into module dashflow)

| Graph | Question it answers | v1 today |
|-------|-------------------|----------|
| **Card interior data** | Which module port → which slot, with which filters on the query path? | Implicit input↔slot + `bind <slot> …` |
| **Filter wiring** | Which filter *definitions* participate where (card chrome, slot query, host, wire-only)? | `filters` / `filters host`, per-slot `bind`, comments in spec |

Card interior uses **`->` only when endpoints differ** (e.g. `drill_src -> drill`). Repeating the same id twice (`heatmap -> … -> heatmap`) is **not** target syntax — slot is a **sink** named once.

Filter wiring and Studio matrix can share edge IR later; they compile to **card-scoped** structures, not module `DashboardDocument.Dashflow` ([ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md) `ApplyFiltersNode` is the semantic template).

### Interior `apply` nodes (data path)

Named filter-application step on the row path ([ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md) names only — compile semantics unchanged).

**Default (v1):** auto-wire `input <slot>` + `bind <slot> f1, f2` — no graph literals.

**Named apply on one slot** (v2) — slot appears **once**:

```text
input heatmap from daily_peak_concurrent_proxy_heatmap_tz.rows

flow
  apply peak_heatmap on heatmap
    usage_date
    app_name
  end apply
end flow
```

**Shared apply across slots:**

```text
flow
  apply peak_shared
    usage_date
    app_name
  end apply
  use peak_shared on heatmap, drill
end flow
```

**Input alias ≠ slot id** — one explicit feed, not a loop:

```text
input heatmap_rows from daily_peak_concurrent_proxy_heatmap_tz.rows

flow
  feed heatmap_rows to heatmap
  apply peak_heatmap on heatmap
    usage_date, app_name
  end apply
end flow
```

(`feed <inputAlias> to <slotRef>` or `heatmap_rows -> heatmap` — same semantics; prefer `feed … to` when arrow would look like a cycle.)

- `apply <id> on <slotRef>` attaches filter list to that slot’s query path; input is auto-wired when aliases match, else `feed … to`.
- v1 `bind <slot> …` desugars to anonymous `apply` on that slot.

Runtime: still `BoundFilters` per slot + `QueryCompiler`; v2 preserves IR as an explicit `CardInteriorFlowGraph` for Designer and diff-friendly specs.

### Filter wiring graph (UI + query participation)

Filter **definitions** (`filter usage_date … bind date column = …`) stay in report scope. The wiring graph records **edges**, not SQL:

| Edge kind | Example | Meaning |
|-----------|---------|---------|
| **chrome** | `usage_date` on card `filters { … }` | Widget placement (local / host / dashboard) |
| **query** | `usage_date` in `apply` / `bind` on slot | Participates in SQL for that slot |
| **wire** | `activity_bucket_time` set on click, bind only on `drill` | Value propagation without heatmap SQL ([LUS overview](samples/demo/) pattern) |

Authoring target (sketch):

```text
filterwire
  usage_date -> chrome peak_concurrent_proxy
  usage_date -> query peak_heatmap
  app_name -> query peak_heatmap
  activity_bucket_time -> wire activity_5min.drill
end filterwire
```

Or interior-only edges inside `card … flow` once `apply` nodes exist. **Non-goal:** duplicate `filter … bind column` in the graph — column binding remains on the filter definition ([ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md)).

### Phasing

| Phase | Deliverable |
|-------|-------------|
| **v1** (done) | Auto-wire inputs↔slots; `bind <slot>` inside `flow` |
| **v2a** | `apply <id> on <slot>` (+ optional `use` / `feed … to`); desugar v1 `bind` |
| **v2b** | Optional `filterwire` (or module-level wiring block) for chrome/query/wire edges; Studio matrix |
| **v2c** | Core round-trip `CardInteriorFlowGraph` + filter wiring on `CardDefinition` |

### Open questions

- Should `filters host` collapse into wiring edges (host card as chrome hub)?
- Same `apply` node referenced from two slots (one SQL policy) vs copy per slot?
- Report-level graph vs card-level only for wire-only filters shared across cards?
