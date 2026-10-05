# DASHSPEC-ADR-0088: Composable flows (BPMN-style subprocess)

| | |
|---|---|
| **Status** | Accepted (concept + IR contract; parser phased) |
| **Date** | 2026-10-05 |
| **Relates to** | [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md), [ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md), [ADR-0083](DASHSPEC-ADR-0083-dataflow-engine-cockpit-transport.md), [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md), [ADR-0036](DASHSPEC-ADR-0036-end-blocks-page-toolbar.md) |

## Context

Module data plane is a **single resolved `FlowGraph`** ([ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md)): sources, transformers, wires, later `apply_filters` for the report slice. Authors need **reuse and readability** when the graph grows:

```text
source -> 1 -> 2 -> 3 -> 4
```

The middle segment `1 -> 2 -> 3` is a natural **named subprocess** (BPMN collapsed subprocess): same semantics as the outer graph, but packaged with a stable **boundary** (one logical input, one logical output) so the parent graph can say `source -> example -> 4`.

Today: one `@flow` per file, no **reference** to another flow as a node in the same graph.

## Decision

### Vocabulary vs keywords

| Term (docs / Studio) | Grammar keyword |
|----------------------|-----------------|
| **Subflow**, subprocess, composite flow | **`flow`** only — no `subflow` keyword (no alias surface) |

“Subflow” describes the **role** (named fragment with boundary ports). Authoring always uses **`flow <id> … end flow`** ([ADR-0036](DASHSPEC-ADR-0036-end-blocks-page-toolbar.md)).

### Two equivalent shapes (same IR)

**1. File root** (existing, [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md)):

```text
@flow stakeholder_peak
  …
end flow
```

**2. Nested named fragment** in the same file (or inline in a tab module body):

```text
@flow executive

source raw { … }

flow calendar
  transformer one { … }
  transformer two { … }
  one -> two
end flow

raw [kpi] -> [in] calendar
calendar [out] -> [in] publish

end flow executive
```

Shorthand for a link-only inner body (no new node kinds) — still closed by `end flow`:

```text
flow example
  one -> two -> three
end flow
```

(`one`, `two`, `three` are transformer/source **ids** declared inside the block or forward-declared; exact ordering rules follow the same port/default rules as the outer graph.)

**3. Separate file** — reuse without nesting:

```text
!include "fragments/calendar.dashflow"   # @flow calendar inside
```

Parent graph references **`calendar`** as a composite node (see below).

### Parent graph: subprocess as one node

In the **parent** link list, a **flow id** used where a node id is expected denotes a **composite** (subprocess reference), not a new runtime node kind:

```text
source [out] -> [in] example
example [out] -> [in] four
```

Sugar when each side has a single default port:

```text
source -> example -> four
```

Meaning:

- `example` is the **boundary** of the inner `flow example { … } end flow` (or of `@flow example` from an include).
- Outer edges attach to **boundary input** / **boundary output** of that fragment.

### Entity-owned default flow

Every **flow-bearing entity** may carry its own **default flow** — the subprocess you get when you open that entity in the Data Flow Designer without naming a separate file:

| Entity | Default flow id (illustrative) | Typical contents |
|--------|-------------------------------|------------------|
| **Tab / module** | same as `@flow` root or `connect { flow … }` | sources, shared transformers, ports for reports |
| **Report** | `report` (implicit) | `apply_filters`, fan-out to card inputs, report-only transforms |
| **Card** | `card.<cardId>` (implicit) | optional micro-steps between `input` and diagram (v2+); v1 may be empty (single `input` only) |

Authoring does **not** require a second vocabulary: the default flow is still a **`flow <id> … end flow`** block, either nested under the entity in the module file, in a sidecar `.dashflow`, or **materialized only in IR** when Studio extracts a selection (see boundary inference).

Parent graphs reference named subprocesses (`source -> example -> four`); **entity default** subprocesses are wired automatically (report cards `input from …` resolve against the report or module graph without repeating the outer shell).

### Boundary ports — explicit or inferred

Boundaries are the cut surface when a subgraph becomes `flow example`. Same philosophy as **`use provider infer`** / **`datasource infer`**: structure may imply the contract; authors may still spell ports when ambiguity or documentation matters.

**Inference (default when wrapping a selection or a link-only `flow` block):**

1. **Ingress** — inner nodes with an incoming edge from **outside** the fragment: each distinct `(outsideNode, outsidePort) → (insideNode, insidePort)` becomes a **boundary input** (merged to one default input when there is exactly one such edge and types agree).
2. **Egress** — inner nodes with an outgoing edge to **outside**: symmetric **boundary output** (single default when unique).
3. **Link-only body** (`flow example` then `1 -> 2 -> 3`) — treat as induced subgraph on `{1,2,3}`: entry = nodes with no inner predecessor among `{1,2,3}`; exit = nodes with no inner successor; wire parent `source -> example` to entry default input, `example -> four` from exit default output.
4. **Ambiguity** (multiple ingress/egress with incompatible types) — resolver error, or require explicit `boundary input` / `boundary output` on the `flow` block.

**Explicit (optional authoring):**

```text
flow example
  boundary input raw
  boundary output localized
  …
end flow
```

| Port | Rule |
|------|------|
| **Input** | Explicit `boundary input` / `default boundary input`, else **inferred ingress** (above), else default input of unique entry node. |
| **Output** | Explicit `boundary output`, else **inferred egress**, else default output of unique exit node. |

Inner nodes keep **internal** port names; parent wires never use `example/two` syntax in v1 — only `example` + optional `[in]` / `[out]` brackets when multiple boundary ports exist.

Typecheck ([ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md)): inferred boundaries get types from the **cut edges**; must match outer wires after composition.

### IR

Resolved graph is still one `FlowGraph`, with two equivalent representations (tooling picks one; executor accepts both):

1. **Collapsed:** `FlowCompositeNode { Id, InnerFlowId, BoundaryIn, BoundaryOut }` + stored inner `FlowGraph` for Designer / step debug.
2. **Flattened:** inner nodes renamed to stable qualified ids (`example/one`, …) and edges rewired — **same executor** as today ([ADR-0083](DASHSPEC-ADR-0083-dataflow-engine-cockpit-transport.md)).

Compilation **must not** duplicate SQL sources when flattening; cache keys use logical `(flowNodeId, outputPort, filter snapshot)` from [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md).

### Report and card (where subprocesses live)

| Layer | Default flow | Named subprocess |
|-------|--------------|------------------|
| Module `@flow` | module graph | `flow calendar`, includes, … |
| Report | implicit `flow report` (filters → shared rowsets) | optional nested `flow …` slices |
| Card | implicit `flow card.<id>` (often empty / single input) | micro-pipeline before diagram when needed |

Card **diagram** stays present plane; it is not a flow node. Card **input** wires to a port on the **enclosing** graph (module or report default flow), not to SQL.

Studio **extract subprocess** = create `flow <id>` from selection + **infer** boundary ports from the cut; refactor parent edges to `… -> <id> -> …`.

### Non-goals (this ADR)

- New keyword `subflow` / `end subflow`.
- Separate runtime engine per nested flow.
- Automatic nesting of card `datasource` into inner flows (legacy compile-to-anonymous-source remains [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md) until migrated).

## Phased delivery

| Phase | Deliverable |
|-------|-------------|
| **C0** (this ADR) | Terminology, boundary contract, parent `source -> example -> four` sugar, inferred boundaries |
| **C1** | Parse nested `flow <id> … end flow` in same `@flow` file; IR `FlowCompositeNode` |
| **C1b** | Entity default-flow metadata in IR; report/card scope for `input` resolution |
| **C2** | `!include` + reference by id; flatten + typecheck; boundary inference in resolver |
| **C3** | Designer extract/collapse subprocess; executor subgraph preview |

**Depends on:** P3b card `input` wiring (done); graph executor beyond source-backtrack (in progress).

## Example (illustrative)

```text
@flow pipeline

source ingestion {
  use provider infer
  from view demo.v_raw
  ports
    output stream raw: RawRow
  end ports
}

flow enrich
  transformer one { ports input stream raw: RawRow; output stream mid: MidRow end ports … }
  transformer two { ports input stream mid: MidRow; output stream rich: RichRow end ports … }
  one -> two
end flow

transformer publish {
  ports
    input stream rich: RichRow
    output stream published: RichRow
  end ports
  transform use project { … }
}

ingestion [raw] -> [in] enrich
enrich [out] -> [in] publish

end flow pipeline
```

Equivalent parent sugar after boundary defaults exist:

```text
ingestion -> enrich -> publish
```

## Consequences

- Authors can name and reuse pipeline segments without copy-paste `datasource` or duplicate transformers.
- Entities **own** a default flow slice; card/report are not second-class — they participate in the same graph with inferred cut surfaces.
- Studio gains **extract subprocess** (infer in/out) and **collapse** (`flow id` box).
- Parser and resolver gain a **composition + boundary inference** pass before `FlowGraph.typeCheck`.
