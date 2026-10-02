# DASHSPEC-ADR-0083: Dashflow — implement via federation **DataFlow** model

| | |
|---|---|
| **Status** | Accepted (architecture; implementation phased) |
| **Date** | 2026-10-02 |
| **Relates to** | [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md), [ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md), [ADR-0082](DASHSPEC-ADR-0082-dashspec-sdk.md) |

> Legacy filename `…cockpit-transport`. Normative: **DataFlow hyperlane** (below Cockpit). **MCP is out of scope** for this ADR.

## Context

Recent DashSpec ADRs ([0078](DASHSPEC-ADR-0078-dashflow-data-plane.md)–[0081](DASHSPEC-ADR-0081-type-plugins.md)) describe:

- typed **in/out ports** on a **`FlowGraph`**;
- **transform plugins** as graph steps;
- **Data Flow Graph** (read-only) and **n8n-like Designer** (palette, wires, per-node preview);
- card **`input`** wiring from graph outputs.

These must be **one implementation**, not a custom DashSpec runtime plus a separate “studio engine.”

Cascade IDE already has that substrate (mis-packaged under `Cockpit/` in-repo):

| CIDE ADR | **DataFlow** role |
|----------|-------------------|
| **0094** ingestion / transport | async delivery, backpressure between producers and consumers |
| **0099** `IDataBus` | typed in-process events |
| **0097** CCU | compute step: raw/stream → **DTO batch** on an output |

**Cockpit** ([0036](https://github.com/AI-Guiders/cascade-ide/blob/develop/docs/adr/0036-cds-channel-compositor-surface-pipeline.md) CDS, instrument strips) sits **above** and is **optional** for DashSpec BI — Host cards consume **port batches** directly.

## Decision

### One model, three surfaces (same stack)

| Surface | What it is | Shared underneath |
|---------|------------|-------------------|
| **Authoring text** | `.dashflow`, `.dashchannel`, card `input` | F# `FlowGraph` IR ([0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [0079](DASHSPEC-ADR-0079-dashflow-type-system.md)) |
| **Authoring visual** | n8n-like Designer, matrix wiring ([0078](DASHSPEC-ADR-0078-dashflow-data-plane.md) P4) | **Same IR** — edit graph, not a parallel schema |
| **Runtime** | Channel nodes + transform plugins + filter nodes | **DataFlow executor**: graph = DAG of CCU-like steps on **DataBus** + typed **port batches** |

No third “presentation runtime” for diagrams vs data: **diagram binds to card `input`**; data reaches inputs only through **executed graph ports**.

```text
Modeling (F#)     compile  →  FlowGraph IR
                                ↓
Execution         run      →  per-node: source | transform plugin | apply_filters
                                ↓ DataBus (lifecycle) + port batches (payload)
Host / Designer   observe  →  FlowRunSnapshot + schema preview + row preview per node
```

### Graph node = DataFlow step

| Dashflow IR | DataFlow primitive |
|-------------|-------------------|
| **Channel node** | Source step (connector DAL) → output port batch `rows R` |
| **Transformer node** | CCU — [ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md) plugin, N inputs / M outputs |
| **Edge** | Typed port wire — validated in Modeling; runtime moves batch ref or cache key |
| **Card `input`** | Consumer port on graph boundary (sink or named export) |
| **Designer “preview this node”** | Run subgraph or single node; subscribe to `PortDataReady` + [0079](DASHSPEC-ADR-0079-dashflow-type-system.md) schema |

**n8n-like UI** is a **view/controller over `FlowGraph` + live `FlowRunSnapshot`** — same types CIDE would use for any DataFlow graph editor pattern, domain-specific node palette only.

### Federation packages (target)

| Layer | Repo | Target `PackageId` |
|-------|------|---------------------|
| Modeling | `guiders-fsharp` | `AIGuiders.Platform.Modeling.DataFlow` (event shapes, port batch metadata, graph-run snapshot schema) |
| Execution | `guiders-platform` | `AIGuiders.Platform.Execution.DataFlow` (DataBus, transport, executor helpers, abstractions) |

Interim pins may use `*.Cockpit.DataBus` until rename; behavior is **DataFlow**, not cockpit semantics.

**DashSpec planet:** `DashSpec.Modeling.*` owns **dashflow grammar + `FlowGraph` DU**; **does not** fork bus/CCU — pins federation DataFlow packages in `DashSpec.Execution.*`.

**CIDE:** migrate `CascadeIDE.Cockpit/DataBus`, ComputingUnits, ingestion into platform **DataFlow**; Cockpit folder keeps CDS/composition/instrument channels only.

### Out of scope here

- **MCP**, agent tools, MCPlane — separate planet/habitat concerns; they may *read* dashboards later but **do not define** dashflow execution.
- **Cockpit** projection of report UI — only if we explicitly build a cockpit-style shell; default Host path is Blazor + DataFlow subscriptions.

## Phased delivery (DashSpec)

| Phase | Deliverable |
|-------|-------------|
| **E0** | Graph executor on DataFlow bus; port batches; Host refresh from outputs |
| **E1** | Read-only Data Flow Graph from IR + `FlowRunSnapshot` |
| **E2** | Designer: palette, typed port drag-wire, per-node preview — **same executor** |
| **E3** | Federation package rename; shared conformance with CIDE DataFlow |

Align with [0078](DASHSPEC-ADR-0078-dashflow-data-plane.md) P1–P4: parser and Studio are **front-ends**, not alternate runtimes.

## Non-goals

- A DashSpec-only event system parallel to DataBus.
- Designer IR ≠ `FlowGraph` IR.
- Implementing dashflow through Cockpit CDS or MCP snapshots.

## Consequences

- **0078–0081** features land as **graph + DataFlow**, one roadmap.
- Transform/type plugins ([0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md), [0081](DASHSPEC-ADR-0081-type-plugins.md)) are **nodes and port types** on the same engine.
- CIDE and DashSpec can share **executor and bus** code; only node catalogs and DSL differ.
