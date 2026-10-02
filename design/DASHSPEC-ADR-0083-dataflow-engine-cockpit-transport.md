# DASHSPEC-ADR-0083: Data Flow Engine — reuse cockpit transport (CIDE / Federation)

| | |
|---|---|
| **Status** | Accepted (architecture; implementation phased) |
| **Date** | 2026-10-02 |
| **Relates to** | [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md), [ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md), [ADR-0082](DASHSPEC-ADR-0082-dashspec-sdk.md) |

## Context

[ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md) defines **`FlowGraph`** (channels, transformers, typed ports) and Execution that runs it. Cascade IDE / Glass already implement a **mature in-process comm stack** (DataBus, ingestion transport, CCU, channel DTOs, CDS snapshots, optional intercom) — see `cascade-ide` ADR 0094–0099, 0036, 0097.

Reinventing ad-hoc events (`Task`, `IProgress`, private Host callbacks) in DashSpec would duplicate that work and break **agent parity** (MCPlane / Cockpit hyperlanes on the Federation map).

## Decision

### One engine, two meanings of “channel”

| Term | Dashflow (this ADR) | Cockpit (CIDE) |
|------|---------------------|----------------|
| **Channel** | **Data plane port** — `rows R` leaving a graph node | **UI strip stream** — DTO for a cockpit instrument |
| **Reuse** | Same **patterns**, not the same DTO types | Reference implementation |

Data Flow Engine **does not** embed Avalonia or PFD/MFD. It **reuses the transport model**:

```text
Connector fetch (DAL)  →  Transform plugin (CCU)  →  Typed row batch (channel DTO)
         ↑                        ↑
    node started/done         DataBus / federation bus events
         ↓
Graph run snapshot (CDS-analog)  →  Host refresh, Designer preview, MCP pulse
```

### Mapping FlowGraph runtime → cockpit layers

| CIDE layer | Data Flow Engine role |
|------------|----------------------|
| **DAL / connector** | `ChannelNode` execution — `IDataSourceConnector.QueryAsync` ([ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md)) |
| **Ingestion transport** | Async pipeline, backpressure, cancellation between nodes (long SQL, large rowsets) |
| **CCU** | **`IDataFlowTransform`** + builtins — pure-ish step: `rows R` → `rows R'` ([ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md)) |
| **Channel DTO** | **Typed port payload** — materialized `rows R` + schema hash; cache key `(nodeId, port, filter snapshot)` |
| **DataBus** | `Publish`/`Subscribe` for **graph lifecycle**: `NodeStarted`, `NodeCompleted`, `NodeFailed`, `PortDataReady`, `GraphInvalidated` |
| **CDS-analog** | **`FlowRunSnapshot`** — not full row data: active graph id, per-node status, port **types**, row counts, last error span — for Designer + agent |
| **Intercom** | Only if Execution runs **out-of-process** (future Studio worker); in-process Host uses DataBus only |

Modeling (F#) stays **outside** the bus: compile-time `FlowGraph` only. Runtime emits events; Host/Designer/MCP **subscribe**.

### Federation packages (already named — not `Modeling.Communication`)

Hyperlane split matches [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md):

| Layer | Repo | Packages (examples) |
|-------|------|---------------------|
| **Modeling** (schemas, rules, event shapes) | `guiders-fsharp` | `AIGuiders.Platform.Modeling.Cockpit.DataBus`, `.Cockpit.Cds`, `.Cockpit.Channels`, `.Cockpit.Composition`, `.Cockpit.Ids`, `.Cockpit.Rules` |
| **Execution** (runtime, DI, adapters) | `guiders-platform` | `AIGuiders.Platform.Execution.Cockpit.Abstractions`, `.Cockpit.DataBus`, `.Cockpit.Channels`, `.Cockpit.Cds`, `.Cockpit.Composition`, `.Cockpit.Transport`, `.Execution.MCPlane` |

`Execution.Cockpit.DataBus` is intentionally thin; event SSOT lives in **F#** `Modeling.Cockpit.DataBus` (`UseGuidersModelingCockpitDataBus` in `eng/Guiders.Modeling.props`).

**CIDE / Glass:** today still carry **`CascadeIDE.Cockpit.*`** in-repo (reference impl). Target: **pin platform Cockpit packages** and shrink CIDE to composition + UI — same trajectory as `AIGuiders.Platform.CommandPlane` (CIDE already pins / `UseLocalGuidersPlatform`).

**Dashflow-specific Modeling** (optional new grain): `AIGuiders.Platform.Modeling.Cockpit.DataFlow` — `FlowRunSnapshot`, graph lifecycle events, port schema refs — **or** extend `Modeling.Cockpit.DataBus` event catalog with a `dataflow/` namespace. DashSpec `FlowGraph` IR itself stays **`DashSpec.Modeling.*`** (planet DSL); only **transport + agent snapshot** are federation Cockpit.

| Phase | Transport |
|-------|-----------|
| **P0** | DashSpec.Execution pins `Execution.Cockpit.DataBus` + `Abstractions`; no fork of `IDataBus` |
| **P1** | Register dashflow events in Modeling.Cockpit.DataBus; MCPlane pulse for graph runs |
| **P2** | Conformance vectors; CIDE deletes duplicate bus when on same package versions |

DashSpec Host is a **planet consumer**, not owner of the bus SSOT — same as [ADR-0082](DASHSPEC-ADR-0082-dashspec-sdk.md) for plugins.

### What stays dashflow-specific

- **Port typing** ([ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md)) — compile-time edge check; cockpit DTOs carry `DashType` manifest reference, not IDE Health segments.
- **Filter wiring** — explicit graph nodes (`apply_filters`), not dashboard UI events alone.
- **Cache / invalidation** — keyed by flow node + filter snapshot, not IDE workspace stratum.

## Phased delivery

| Phase | Deliverable |
|-------|-------------|
| **E0** | `FlowRunSnapshot` + event types in Execution.Runtime; Host subscribes to refresh cards |
| **E1** | Data Flow Graph UI reads snapshot + port schemas (no full n8n editor required) |
| **E2** | Pin shared bus package from guiders-platform / CIDE extraction; MCP tool exposes graph pulse |

## Non-goals

- Merging DashSpec **data channel** grammar with **IDE Health** channel code in one assembly.
- Shipping full `CascadeIDE.Cockpit` inside `DashSpec.Host` NuGet.
- Using DataBus for **authoring** — only **execution observability** and refresh orchestration.

## Consequences

- Data Flow Engine implementation **tracks** CIDE cockpit evolution instead of a second event story.
- Transform plugins ([ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md)) are natural **CCUs** — testable units with channel DTO in/out.
- Studio / agent see the same graph state humans infer from “what data is wired” — CDS-style snapshot, not `Dictionary<string, object?>` dumps.
