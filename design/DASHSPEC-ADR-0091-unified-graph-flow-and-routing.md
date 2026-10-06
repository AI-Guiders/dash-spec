# DASHSPEC-ADR-0091: Unified Graph — flow vs routing (single syntax)

| | |
|---|---|
| **Status** | Accepted (concept + IR direction; parser phased) |
| **Date** | 2026-10-06 |
| **Relates to** | [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md), [ADR-0088](DASHSPEC-ADR-0088-flow-composition-subprocess.md), [ADR-0090](DASHSPEC-ADR-0090-card-interior-flow.md), [ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md) |

## Context

DashSpec accumulated **several wiring stories** with overlapping keywords:

| Today | What is wired |
|-------|----------------|
| `.dashflow` links | `TypedRowBatch` between sources / transformers |
| `input … from node.port` | Module output → card consumer |
| `bind` / card `flow bind` | Filter **names** → slot SQL participation |
| `filters` / `filters host` | Filter **chrome** on card / host |
| `on click set …` | Event → filter value (wire) |

Authors and Studio must learn multiple mental models (**split brain**). The product already uses one **link surface** in dashflow (`producer [out] -> [in] consumer`); card and report wiring should not invent a second arrow language.

## Decision

### One noun: **Graph**

**Graph** is the shared IR:

```text
Graph
├── nodes: GraphNode (id, kind, properties, port signatures)
├── edges: GraphEdge (fromNode, fromPort, toNode, toPort, edgeKind)
└── scopes: module | report | card:<id> | nested subprocess (ADR-0088)
```

- **One authoring syntax** for edges everywhere link lines are allowed (see [FlowLinkParser](../src/DashSpec.Modeling.Parse/DataFlow/FlowLinkParser.fs)).
- **Specializations** are **views** over the same Graph (subgraph + edgeKind filter), not separate file grammars:
  - **Flow graph** — only `edgeKind = flow` (rowset movement).
  - **Routing graph** — only `edgeKind = route` (filter participation, chrome, host, wire).
  - (Optional later) **Event edges** — `edgeKind = event` (click → filter value); may compile into route state, not SQL.

Text roots (`@flow`, `card … flow`, future `route` blocks) are **containers** that populate the same Graph IR with a scope tag.

### Split: **flow** vs **route**

| | **Flow** | **Route** |
|---|----------|-----------|
| **Carries** | `TypedRowBatch` (typed rows, [ADR-0079](DASHSPEC-ADR-0079-dashflow-data-plane.md)) | Filter **identity** + role (query / chrome / wire) — not a rowset |
| **Compile** | SQL + transformers (`QueryCompiler`, `DashflowBatchExecutor`) | `BoundFilters`, chrome layout, host inheritance, click targets |
| **Typical nodes** | `source`, `transformer`, nested `flow` (ADR-0088) | `filter` (report), `slot`, `chrome`, `card` |
| **Typical ports** | `rows`, `raw`, `out` | `value` or name-aligned `in usage_date` on slot |
| **Example** | `peak_tz.rows -> [rows] heatmap` | `app_name -> [app_name] heatmap` |

**Rule:** do not encode route semantics as flow transforms (no `transformer` that “applies filters” on rows in module flow). Filter columns stay on the **filter node** ([ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md)); **route edges** say which slot queries use which filter.

### Nodes (sketch)

| Node kind | Scope | Role |
|-----------|-------|------|
| `source`, `transformer` | module | Flow only |
| `filter` | report | Definition + `out` route port(s) |
| `slot` | card | Consumer: `in rows` (flow) + named `in <filterId>` (route) |
| `chrome` | card / report | Toolbar / card filter bar (route target) |
| `card` | report | Container; may expose boundary ports |

**Slot** declares ports explicitly (authoring target):

```text
slot heatmap
  diagram …
  ports
    in rows
    in activity_slot
    in app_name
  end ports
end slot
```

### Unified link syntax (normative surface)

Same line shape for flow and route (parser infers `edgeKind` from port types, or from container default):

```text
producer [outPort] -> [inPort] consumer
```

Examples on one card (`activity_5min`):

```text
// flow (module → slot)
five_minute_activity_drilldown_tz.rows -> [rows] heatmap

// route (filter → slot query port)
activity_slot -> [activity_slot] heatmap
app_name -> [app_name] heatmap
activity_bucket_time -> [activity_bucket_time] drill   // wire + query on drill only

// route (filter → chrome) — report/card scope
activity_slot -> chrome activity_5min
```

No `heatmap -> heatmap` loops: **slot is a sink**; producers are **module outputs** or **filter nodes**.

### Scopes and composition

| Scope | File / block | Contains |
|-------|----------------|----------|
| `module` | `@flow` / `connect { flow … }` | Flow graph only (sources, transformers, flow edges) |
| `report` | `@tab` / `report` | Filter nodes; optional report-level route edges (dashboard chrome) |
| `card:<id>` | `card …` | Slot nodes; flow + route edges crossing card boundary |

Module flow **does not merge** into card scope in the IR as one flat graph; **edges** reference nodes across scopes by id (card boundary = foreign port on module node).

Subprocess ([ADR-0088](DASHSPEC-ADR-0088-flow-composition-subprocess.md)) = nested **flow** subgraph with boundary ports; same Graph model.

### Desugar (migration)

| Legacy | Graph |
|--------|--------|
| `source …` + `a -> b` in `.dashflow` | flow nodes + flow edges |
| `input heatmap from node.rows` | `node.rows -> [rows] heatmap` (card scope) |
| `flow bind heatmap f1, f2` | `f1 -> [f1] heatmap`, `f2 -> [f2] heatmap` (route edges; port names = filter ids) |
| `filters { f1, f2 }` on card | `f1 -> chrome <card>`, … (route) |
| `filters host H { f1 }` | route edges host chrome ↔ filters + policy |
| `on click set X from y` | event edge `heatmap.click.x -> [value] X` (or route wire annotation on slot) |

[ADR-0090](DASHSPEC-ADR-0090-card-interior-flow.md) v1 (`bind` inside `flow`) is **routing sugar** until port declarations land.

### Studio / executor

- **Data Flow Designer** — view: `edgeKind = flow` (+ typed ports).
- **Routing / filter wiring** — view: `edgeKind = route` (matrix: filters × slots × role).
- Executor unchanged in principle: materialize flow paths → SQL; collect route edges → `BoundFilters` per slot.

## Consequences

- [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md) **FlowGraph** becomes `Graph` restricted to flow edges (rename in IR when implemented; dashflow file = flow subgraph).
- Card interior is **not** a second platform; it is **routing + boundary flow edges** into `slot` nodes.
- New keywords should prefer **declaring ports** and **link lines**, not duplicate ids (`bind` lists, `input for` when alias = slot).

## Phased delivery

| Phase | Deliverable |
|-------|-------------|
| **P0** (this ADR) | Terminology: Graph, flow vs route, one link syntax, desugar table |
| **P1** | IR: `Graph` + `edgeKind`; flow subgraph = current dashflow parse |
| **P2** | `slot` + `ports`; route edges; desugar `bind` → edges |
| **P3** | Chrome/host/event route edges; Studio routing view |
| **P4** | Optional single report-level graph export for documentation |

## Non-goals

- One physical file for entire report graph (module + all cards) — scopes may stay in separate authoring roots linked by ids.
- Routing edges that execute SQL or row transforms.
- Replacing filter **definitions** (`bind date column = …`) with edges.

## Open questions

- Explicit `route` block vs shared `flow` block with inferred edgeKind from ports?
- Port naming: `in usage_date` vs generic `in` + bracket name on edge only?
- Event edges: first-class in Graph or desugar to `on click` until P3?
