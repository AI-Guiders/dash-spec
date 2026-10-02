# DASHSPEC-ADR-0078: Dashflow — data plane (channels, transformers, card inputs)

| | |
|---|---|
| **Status** | Accepted (concept + IR; parser / runtime phased) |
| **Date** | 2026-10-02 |
| **Relates to** | [ADR-0006](DASHSPEC-ADR-0006-sql-datasource-and-sqldialect.md), [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md), [ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md), [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md), [ADR-0018](DASHSPEC-ADR-0018-sql-datasource-carriers.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md), [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md), [ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md) |

## Context

### Original model

DashSpec assumed **warehouse ETL in the DB**: views (`schema.v_*`) hold domain logic; the report **displays** filtered rows ([ADR-0006](DASHSPEC-ADR-0006-sql-datasource-and-sqldialect.md)). `connector` + `datasource view` on each **card** kept that story visible in one file.

### BI drift

The product evolved toward **BI semantics** without a named data layer:

| Concern | Where it lives today | Nature |
|---------|----------------------|--------|
| SQL extract + `bind` filters | `QueryCompiler`, card `datasource` | channel-like, but per card |
| UTC facts → reporting calendar / TZ | `LabelFormat.DisplayTimeZone`, `DateValueCodec`, report `date_format` | **semantic transform**, not SQL |
| Axis order on formatted labels | `LabelFormat.TryParseChronological`, `MatrixPayloadBuilder` | transform + present blurred |
| Hour / time grid gap-fill | `TimeSeriesGrid`, `MatrixPayloadBuilder.BuildHourGrid` | **shape transform** on rowset |
| Top-N series | `transform series` on card ([ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md)) | viz-adjacent, should share outputs |

Result: **two ETL layers** (DB + Execution.Runtime), the second **implicit**, not reusable across cards, hard to review in spec or Studio.

### Goal

Split **data flow** from **presentation**:

- **Channel** — boundary to external data; declares **typed outputs** ([ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md)) and optional **param surface** for filters (types only).
- **Transformer** — N inputs → M outputs; declarative steps (including time/TZ/grain).
- **Card** (and optionally sheet/report) — declares **inputs** wired to flow ports; **diagram / present** only.

Optional sugar: `use channel` on a card when one input maps 1:1. Legacy `datasource` on card compiles to an anonymous inline channel (migration path, not dashspec-2 default).

## Decision

### Planes (normative)

```text
[ Connector plugins ]     infrastructure (credentials, dialect) — ADR-0001
        ↓
[ Dashflow graph ]        channels + transformers + wires
        ↓
[ Report / sheet / card ] inputs + diagram + presentation
```

| Plane | Owns | Does not own |
|-------|------|----------------|
| **DB** | Domain facts, agg, views, UTC storage | UI formats |
| **Channel** | `from view` / `from sql`, param surface (filter **types**), **typed** `output` | `diagram`, filter **values** |
| **Transformer** | Steps on rowsets (semantic time, aggregate, limit, …) | SQL connection strings |
| **Present** | `diagram`, `presentation`, viz plugins, human labels (`as`) | Ad-hoc SQL per card |

**Rule:** stable, heavy logic → **view in DB**. Report-specific reshape or prototype → **transformer** in dashflow. If a transformer outlives two reports or is expensive → migrate to view.

### IR: `FlowGraph` (Modeling SSOT)

Execution resolves a single graph per module (or shared included flow):

```text
FlowGraph
├── nodes: ChannelNode | TransformerNode
├── edges: (producerPort → consumerPort)
└── metadata: bind names propagated to channel nodes
```

| Node | Ports |
|------|--------|
| **Channel** | outputs only (≥1); each port typed `rows R` ([ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md)); optional param surface for filter types |
| **Apply filters** (report graph) | row `in` / `out` + filter value ports; wires dashboard filters explicitly |
| **Transformer** | inputs (≥1), outputs (≥1) |
| **Card** (consumer, in report IR) | inputs (≥0); each input references `(nodeId, outputPort)` + column mapping to diagram |

Authoring may be **declarative** (`.dashflow` text) or **matrix** (Studio: consumers × producer outputs). Both compile to the same `FlowGraph`.

### File kinds (extends [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md))

| Extension | Root | Contents |
|-----------|------|----------|
| `.dashchannel` | `@channel <id>` | optional standalone channel library entry |
| `.dashflow` | `@flow <id>` | channels, transformers, wires |

Module wiring ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md)):

```text
wiring {
  use connector sqlserver
  flow "flows/stakeholder.dashflow"
}
```

`.dashtransform` remains **presentation / series chrome** ([ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md)) — **not** a dashflow data transformer. Data steps use `transformer` inside `.dashflow` only.

### Channel (sketch grammar)

```text
type UtilizationRow = record {
  user_sam: string
  usage_day: local_date
  concurrent_apps: int
}

channel utilization {
  from view lus.v_daily_peak_concurrent_apps_per_user
  params { usage_date: date_range, app_name: field_set<string> }
  output utilization: rows UtilizationRow
}
```

- `from` — `view` | `sql query` | `sql file` ([ADR-0018](DASHSPEC-ADR-0018-sql-datasource-carriers.md)).
- **Output** is the stable typed API for downstream transformers and card inputs.
- Filter **values** connect in the **report internal flow** (`apply_filters`), not on the channel node.

### Transformer (sketch grammar)

```text
transformer reporting_calendar {
  input raw from utilization.utilization
  output localized {
    step semantic_time {
      reporting_zone = Europe/Moscow
      columns {
        bucket_start_utc → usage_day_local (date)
        bucket_start_utc → sort_key (instant)
      }
    }
  }
}
```

Closed **step catalog** (v1 subset, grow explicitly per ADR):

| Step kind | Purpose | v1 |
|-----------|---------|-----|
| `semantic_time` | UTC → reporting zone; calendar columns; sort keys vs display | **yes** (formalizes today’s `LabelFormat` + report defaults) |
| `project` / `rename` | Column select/rename | yes |
| `aggregate` / `group` | Group-by measures | deferred |
| `sort` / `limit` | Top-N rowsets | deferred |
| `join` | Multi-input | deferred (IR allows N inputs) |
| `series_top` | Move from card `transform series` | optional v1.1 |

### Card inputs (sketch grammar)

```text
card peak_table as "Top users" {
  input localized from reporting_calendar.localized
  diagram table {
    columns user_sam, usage_day_local
    formats { usage_day_local = date.short }
  }
}
```

- **`formats` on diagram** — present layer; chronological sort uses data-plane `sort_key` when present (no re-parsing `dd.MM` strings).
- **`use channel X`** — sugar: single implicit `input` from channel default output.

### Implicit transforms today → v1 target

| Current (Execution.Runtime) | Target node |
|-----------------------------|-------------|
| `LabelFormat` + `DisplayTimeZone` + report `date_format` | `semantic_time` transformer (or channel post-step preset) |
| `TimeSeriesGrid` / hour grid fill | `transformer time_grid` (deferred; document as follow-up) |
| Per-card `datasource` | Anonymous `channel __card_<id>` in resolved graph |
| `MatrixPayloadBuilder` axis parse of display strings | Prefer explicit sort columns from `semantic_time` |

Until dashflow lands in the parser, **behavior unchanged**; new features that add post-SQL semantics must be implemented as if they will become a **named transformer step** (no new hidden statics on card path).

## Phased delivery

| Phase | Deliverable |
|-------|-------------|
| **P0** (this ADR) | Terminology, `FlowGraph` contract, file kinds, split from `.dashtransform` |
| **P1** | F# Modeling: `FlowGraph` DU + resolve; compile legacy `datasource` → inline channel |
| **P2** | `semantic_time` step in Execution; wire report `date_format` / host display TZ to one graph node |
| **P3** | `.dashflow` parse + `wiring { flow … }`; card `input` |
| **P4** | Studio matrix authoring; shared flows across tabs; `aggregate` / `join` steps |

**Trigger to start P1:** duplicate `datasource view` on the same view in one tab **or** second consumer needs the same localized stream (LUS executive KPI pattern).

## Future surfaces

- **Data Flow Graph** (read-only) from resolved `FlowGraph` + [ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md) schemas.
- **Data Flow Designer** (n8n-like): palette of node kinds, **typed ports**, per-node preview — same IR as text.

## Non-goals

- Replacing SQL views as primary ETL.
- General-purpose ETL language (Airflow/dbt in spec).
- Parser changes in the same PR as this ADR.
- Dynamic typing or reference types in the flow graph ([ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md)).

## Consequences

- Data reuse and cache keys become **`(flowNodeId, outputPort, filter snapshot)`** instead of per-card query compile.
- BI time/TZ/format policy moves from scattered Runtime helpers to a **declared** graph — testable and visible in includes.
- `.dashtransform` name stays; authors learn **data `transformer`** vs **viz `transform` preset**.

## Example (LUS-oriented, illustrative)

```text
@flow stakeholder {
  channel executive {
    from view lus.v_stakeholder_kpi_executive
    params { usage_date: date_range, app_name: field_set<string> }
    output kpi: rows StakeholderKpiRow
  }

  transformer executive_local {
    input kpi from executive.kpi
    output kpi_local {
      step semantic_time {
        reporting_zone = Europe/Moscow
        date_format = "dd.MM"
      }
    }
  }
}

# In report — many cards:
card kpi_peak as "Peak" {
  input kpi_local from executive_local.kpi_local
  diagram kpi_tile { value = peak_util … }
}
```

One localized stream; many cards; SQL view unchanged.
