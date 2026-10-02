# DASHSPEC-ADR-0083: Dashflow — federation **DataFlow** (separate from Cockpit)

| | |
|---|---|
| **Status** | Accepted (architecture; implementation phased) |
| **Date** | 2026-10-02 |
| **Relates to** | [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md), [ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md), [ADR-0082](DASHSPEC-ADR-0082-dashspec-sdk.md) |

> Legacy filename `…cockpit-transport`. **DataFlow** ≠ **Cockpit**. **MCP** out of scope.

## Context

DashSpec ADRs [0078](DASHSPEC-ADR-0078-dashflow-data-plane.md)–[0081](DASHSPEC-ADR-0081-type-plugins.md) need one stack for typed ports, `FlowGraph`, transform plugins, n8n-like Designer, and runtime — without a second “studio engine.”

Historical confusion: CIDE packaged **bus + compute steps** under `Cockpit/` and named compute steps **CCU** (ADR 0097). Normatively:

| Layer | Question it answers | Vocabulary |
|-------|---------------------|------------|
| **DataFlow** | **What** data moves **where**, how it is **transformed** on the path | **Source**, **Transform**, typed **ports**, **edges**, **bus**, port **batch** |
| **Cockpit** (later, optional consumer) | **Where** a human/agent **sees** meaning (attention, CDS, instrument strips) | **CCU** (fold for display), **cockpit channel**, compositor, surface |

**CCU does not live in DataFlow.** In DataFlow the compute step is a **Transform** ([ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md)). A cockpit instrument may **subscribe** to DataFlow (events + port metadata) and run its **own** CCU to build a strip DTO — that is integration, not part of the data-plane graph.

DashSpec Host (BI) **consumes DataFlow directly** (card `input` ← port batch). Cockpit wiring is **not required** for v1.

## Decision

### Separation (normative)

```text
[ DataFlow ]     sources, transforms, typed ports, executor, bus, FlowRunSnapshot
      ↑
      │  (optional, later)
[ Cockpit ]      CDS, cockpit channels, CCU → instrument DTO, compositor, surface
```

- **DataFlow describes** routing and transformation of **domain payloads** (e.g. `rows R`).
- **Cockpit connects to DataFlow** when a product needs PFD/MFD-style attention — it does not own the graph.

### Terminology mapping (CIDE heritage → federation)

| CIDE / old docs | **DataFlow** (use this) | **Cockpit only** |
|-----------------|-------------------------|------------------|
| ADR 0094 ingestion | **Transport** | — |
| ADR 0099 DataBus | **DataFlow bus** (lifecycle + domain events) | — |
| ADR 0097 **CCU** | **Transform** (plugin / builtin) | CCU = fold **for display channel** |
| “Channel” (data port) | **Port** / **edge** on `FlowGraph` | “Channel” = instrument strip |
| ProjectionGraph (event → field) | **Transform wiring** + run snapshot | cockpit **ProjectionGraph** for IDE Health |

When migrating CIDE/CDP code into platform packages, **rename by layer**: DataFlow packages say **Transform**; Cockpit packages keep **CCU** where the output is a **cockpit channel DTO**.

### One model, three surfaces (DashSpec)

| Surface | Shared underneath |
|---------|-------------------|
| `.dashflow` / `.dashsource` / card `input` | F# **`FlowGraph` IR** + [0079](DASHSPEC-ADR-0079-dashflow-type-system.md) |
| n8n-like Designer | **Same IR**; preview = **same executor** |
| Runtime | **DataFlow executor**: DAG of sources + **transforms**; bus + port batches |

```text
DashSpec.Modeling (F#)   compile  →  FlowGraph IR
DashSpec.Execution       run      →  source | transform use … | apply_filters
                                   →  bus + port batches + FlowRunSnapshot
Host / Designer          observe  →  snapshot + per-node schema / row preview
```

### Modeling reference (graph IR) — **CDP / Ide.Session**, not Cockpit.Channels

**Port typing and edge validation** for `FlowGraph` should follow the same discipline as CDP’s session graph modeling (not IDE Health algebra):

| Reference | Repo / package | Reuse pattern |
|-----------|----------------|---------------|
| Graph DU, relations, validation, patch | `guiders-fsharp` **`AIGuiders.Platform.Modeling.Ide.Session`** (`SolutionGraph`, `GraphValidation`, `GraphPatch`, …) | How CDP models **nodes, edges, validate, diagnose** |
| Dogfood wiring | **`cdp-mcp`** (`CdpMcp` → `Modeling.Ide.Session` + `Execution.Ide.Session`; desk **bus bridge** only at runtime) | Planet F# IR + C# execution split ([ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md)) |

**DashSpec-owned:** dashflow grammar, `FlowGraph` node kinds, `DashType` on ports ([0079](DASHSPEC-ADR-0079-dashflow-type-system.md)). **Federation-owned (target):** generic DataFlow bus/event/batch contracts. **Do not** put `FlowGraph` parse IR in `Modeling.Cockpit.*`.

Cockpit F# (`Modeling.Cockpit.DataBus` / `ProjectionGraph`) remains relevant for **bus event catalogs** and **cockpit-side** projection — not for BI graph modeling.

### DataFlow graph primitives (DashSpec)

| IR / runtime | Role |
|--------------|------|
| **Source** (graph node) | Connector fetch → output port `rows R` |
| **Transform** | [ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md) plugin; N→M typed ports |
| **Edge** | Modeling proves port compatibility; runtime passes batch or cache key |
| **Sink** | Card `input`, export port, or future cockpit subscription point |
| **Bus** | `NodeStarted`, `PortDataReady`, `GraphInvalidated`, … |
| **FlowRunSnapshot** | Run status for Designer/Host — **not** CDS |

### Federation packages (target)

| Layer | `PackageId` (introduce / migrate) |
|-------|-----------------------------------|
| Modeling | `AIGuiders.Platform.Modeling.DataFlow` (+ graph-run schema; **not** CCU types) |
| Execution | `AIGuiders.Platform.Execution.DataFlow` (bus, transport, `ITransform`, executor helpers) |

Interim: `*.Cockpit.DataBus` pins until rename; **semantics** are DataFlow.

**CIDE/CDP migration:** move bus + **transform** execution into **DataFlow**; leave **CCU + cockpit channels + CDS** under **Cockpit**.

### Out of scope

- MCP / MCPlane as part of DataFlow design.
- Requiring Cockpit for DashSpec Host refresh.
- Using **CCU** as a public name in DataFlow APIs or dashflow DSL.

## Phased delivery (DashSpec)

| Phase | Deliverable |
|-------|-------------|
| **E0** | `FlowGraph` modeling (Ide.Session-style validation) + executor + transforms + bus |
| **E1** | Read-only graph UI + `FlowRunSnapshot` |
| **E2** | Designer on same IR + executor preview |
| **E3** | Platform `Modeling/Execution.DataFlow` packages; CIDE/CDP rename split |

## Non-goals

- CCU inside DataFlow layer.
- Cockpit CDS as dashflow SSOT.
- Designer IR ≠ `FlowGraph` IR.

## Consequences

- Clear onboarding: **DataFlow = data plane**; **Cockpit = optional presentation consumer**.
- DashSpec transform plugins align with **Transform**, not CCU.
- Graph modeling aligns with **CDP session-graph** practice; runtime aligns with **shared bus** (CDP desk bridge pattern).
