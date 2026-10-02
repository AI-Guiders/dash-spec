# DASHSPEC-ADR-0083: Data Flow Engine — federation **DataFlow** layer (below Cockpit)

| | |
|---|---|
| **Status** | Accepted (architecture; implementation phased) |
| **Date** | 2026-10-02 |
| **Relates to** | [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md), [ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md), [ADR-0082](DASHSPEC-ADR-0082-dashspec-sdk.md) |

> Filename still says `cockpit-transport` for history; normative name is **DataFlow hyperlane**, not Cockpit.

## Context

[ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md) defines **`FlowGraph`** and Execution that runs it. Cascade IDE already built the **same substrate** for IDE domain signals — but it is **mis-filed under `Cockpit/`** in the repo:

| CIDE ADR | Layer (logical) |
|----------|-----------------|
| **0094** ingestion / `Channel<T>` transport | **DataFlow** — delivery, backpressure |
| **0099** `IDataBus` typed events | **DataFlow** — domain events in-process |
| **0097** CCU → DTO | **DataFlow** — compute step on a stream |
| **0036** channel → **CDS** → compositor → surface | **Cockpit and above** — attention, slots, instruments |

**Cockpit is above:** CDS answers *where* meaning may appear (PFD/MFD, topology); **Data Flow answers *what moved* between producers and consumers** (rows, events, snapshots) — including DashSpec `FlowGraph` and IDE build output alike.

DashSpec must not reinvent this stack; it must **align with the federation DataFlow plane**, then **optionally project** into Cockpit/MCPlane for agents.

## Decision

### Stack (normative)

```text
[ DataFlow ]     ingestion, DataBus, CCU/transform, typed port batches, run snapshots
      ↓ feeds
[ Cockpit ]      CDS, cockpit channels (IDE Health strips), compositor, instruments
      ↓
[ Surface ]      Avalonia / Glass / Blazor Host widgets
      ↓
[ MCPlane ]      agent pulse (truncated observation) — consumes snapshots, not raw rowsets
```

DashSpec **Data Flow Engine** lives entirely in **`[ DataFlow ]`**. Host card refresh subscribes to DataBus / run snapshot; **only if** we embed a cockpit-style designer do we map into CDS-like semantics.

### Mapping `FlowGraph` runtime → DataFlow (not Cockpit)

| DataFlow primitive | DashSpec role |
|--------------------|---------------|
| **Source / DAL** | Connector `ChannelNode` — [ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md) |
| **Transform / CCU** | `IDataFlowTransform` plugins — [ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md) |
| **Port batch** | Materialized `rows R` + schema hash; cache `(nodeId, port, filter snapshot)` |
| **DataBus** | `NodeStarted`, `NodeCompleted`, `NodeFailed`, `PortDataReady`, `GraphInvalidated` |
| **Run snapshot** | **`FlowRunSnapshot`** — statuses, port types, counts, errors — **not** CDS (no PFD/MFD topology) |

Two meanings of **“channel”**:

| | Dashflow | Cockpit (CIDE) |
|--|----------|----------------|
| **Channel** | Typed **data port** on the graph | **Instrument strip** DTO (IDE Health, EICAS, …) |
| **Relation** | Cockpit channel **may subscribe** to DataFlow events and project a strip; DashSpec cards **subscribe** to port batches directly |

### Federation packages (target naming)

**Not** `Modeling.Communication`. **Not** primary home under `Execution.Cockpit.*` (today’s placement is **transitional** while CIDE extracted code).

| Layer | Repo | Target `PackageId` (to introduce / migrate) |
|-------|------|-----------------------------------------------|
| **Modeling** | `guiders-fsharp` | `AIGuiders.Platform.Modeling.DataFlow` (+ `.DataBus`, `.Events`, `.Rules` as needed) |
| **Execution** | `guiders-platform` | `AIGuiders.Platform.Execution.DataFlow` (+ `.DataBus`, `.Transport`, `.Abstractions`) |

**Interim (as-is on NuGet):** `Modeling.Cockpit.DataBus` / `Execution.Cockpit.DataBus` — **same contracts**, rename/split when federation split-audit lands; DashSpec may pin interim ids first, migrate ids without behavior change.

**CIDE / Glass:** peel `CascadeIDE.Cockpit/DataBus`, `…/ComputingUnits`, ingestion helpers into **platform DataFlow** packages; leave under `Cockpit/` only **CDS, Composition, cockpit Channels, Surface**.

**Planet DSL:** `FlowGraph` IR stays **`DashSpec.Modeling.*`**. Federation owns **event shapes + bus + transport**, not `.dashflow` grammar.

**Agent ingress:** `Execution.MCPlane` **reads** `FlowRunSnapshot` / pulse hooks — sits **above** DataFlow, same as cockpit reads IDE Health DTOs.

### Phased alignment

| Phase | Work |
|-------|------|
| **F0** | GUIDERS ADR: formal **DataFlow** hyperlane below Cockpit; map CIDE ADR 0094/0097/0099 |
| **F1** | DashSpec.Execution pins `Execution.DataFlow.*` (or interim `Cockpit.DataBus`); implement graph executor + events |
| **F2** | Move F# event SSOT from `Modeling.Cockpit.DataBus` → `Modeling.DataFlow`; conformance vectors |
| **F3** | CIDE + DashSpec + CDP on same package ids |

## Phased delivery (DashSpec)

| Phase | Deliverable |
|-------|-------------|
| **E0** | `FlowRunSnapshot` + DataBus events in Execution.Runtime; Host refresh |
| **E1** | Data Flow Graph UI from snapshot + [ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md) schemas |
| **E2** | MCPlane projection for graph runs; optional Cockpit designer bridge later |

## Non-goals

- Labeling graph execution as **Cockpit** domain (wrong layer).
- Merging dashflow **ports** with IDE Health **strips** in one DTO type.
- Putting `FlowGraph` parse IR into guiders-fsharp (planet stays DashSpec).

## Consequences

- Data Flow Engine and DashSpec Host share a **substrate** with CIDE build/test/git streams — one bus philosophy, many domains.
- Cockpit/Glass become **consumers** of DataFlow for instruments; DashSpec BI becomes a **first-class DataFlow domain**, not a cockpit special case.
- Renaming packages clarifies onboarding: **DataFlow → Cockpit → Surface → MCPlane**.
