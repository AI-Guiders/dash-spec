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

### Boundary ports (normative)

Each composable `flow <id>` exposes:

| Port | Rule (v1) |
|------|-----------|
| **Input** | If the inner graph declares `boundary input <portName>` / `default boundary input`, use that; else the **default input** of the unique inner sink chain entry (or compile error if ambiguous). |
| **Output** | Symmetric: `boundary output` or default **output** of the inner exit node. |

Inner nodes keep **internal** port names; parent wires never use `example/two` syntax in v1 — only `example` + optional `[in]` / `[out]` brackets.

Typecheck ([ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md)): boundary stream types must match outer edges (same as transformer input/output).

### IR

Resolved graph is still one `FlowGraph`, with two equivalent representations (tooling picks one; executor accepts both):

1. **Collapsed:** `FlowCompositeNode { Id, InnerFlowId, BoundaryIn, BoundaryOut }` + stored inner `FlowGraph` for Designer / step debug.
2. **Flattened:** inner nodes renamed to stable qualified ids (`example/one`, …) and edges rewired — **same executor** as today ([ADR-0083](DASHSPEC-ADR-0083-dataflow-engine-cockpit-transport.md)).

Compilation **must not** duplicate SQL sources when flattening; cache keys use logical `(flowNodeId, outputPort, filter snapshot)` from [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md).

### Report and card (where subprocesses live)

| Layer | Subprocess? |
|-------|-------------|
| Module `@flow` | Yes — ETL / shared streams (`calendar`, `kpi_pipe`, …). |
| Report slice | Yes — e.g. inner `flow report_filters` wrapping `apply_filters` + fan-out to cards ([ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md)). |
| Card | **Consumer** only (`input from node.port`); optional future **card-local** `flow` only for micro-steps before diagram — same composite rules, scoped to card id. |

Card **diagram** stays present plane; it is not a subprocess.

### Non-goals (this ADR)

- New keyword `subflow` / `end subflow`.
- Separate runtime engine per nested flow.
- Automatic nesting of card `datasource` into inner flows (legacy compile-to-anonymous-source remains [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md) until migrated).

## Phased delivery

| Phase | Deliverable |
|-------|-------------|
| **C0** (this ADR) | Terminology, boundary contract, parent `source -> example -> four` sugar |
| **C1** | Parse nested `flow <id> … end flow` in same `@flow` file; IR `FlowCompositeNode` |
| **C2** | `!include` + reference by id; flatten + typecheck |
| **C3** | Designer collapsed/expand subprocess; executor subgraph preview |

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
- Studio gains a natural **collapse** unit (subprocess box = `flow id`).
- Parser and resolver gain a **composition** pass before `FlowGraph.typeCheck`.
