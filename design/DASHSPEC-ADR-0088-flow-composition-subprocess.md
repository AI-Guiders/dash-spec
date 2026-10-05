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

### One module graph — no mandatory “default flow” per entity

[ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md) already resolves **one `FlowGraph` per tab/module**. That is the SSOT; report and card do **not** each require a hidden nested `flow`.

| Entity | Role in data plane |
|--------|-------------------|
| **Module** | Owns the graph (`@flow` / `connect { flow … }`, nested **`flow <id>`** only when the author names a subprocess). |
| **Report** | **Scope** for filters, pages, toolbar — wiring is **nodes and edges in the module graph** (e.g. `apply_filters`), not an automatic implicit `flow report`. |
| **Card** | **Consumer**: `input from node.port` points at a port on the **same** resolved graph; diagram stays present-only. |

Optional: authors may **name** a subprocess when it helps (`flow report_filters { … } end flow`) — that is explicit composition, not a runtime default container per entity.

**Studio** may show a *filtered view* of the same graph (“cards on this report”, “nodes feeding this card”) — a **lens**, not a second flow IR.

**Why skip per-entity default flows:** avoids merging subgraphs, duplicate ids (`card.foo` vs module), and ambiguity about where `apply_filters` lives.

### Nested `flow` = same grammar, composite node in parent

A nested **`flow <id> … end flow`** is not a different language: the body uses the **same** rules as the enclosing `@flow` (sources, transformers, links, further nested `flow` blocks). The only difference from the file root is the **keyword** (`@flow` vs `flow`) and that the block becomes a **`FlowNodeKind.Composite`** in the parent resolved graph.

**Parent-facing ports** are declared explicitly on the nested block (same `ports` / `end ports` envelope as elsewhere, different lines):

```text
flow example
  transformer alpha { … }
  transformer beta { … }
  transformer gamma { … }
  alpha -> beta -> gamma
  ports
    input p1
    output p2
  end ports
end flow
```

| Piece | Meaning |
|-------|---------|
| **`p1` / `p2`** | Parent-facing names in links (`… -> [p1] example`). |
| **Entry / exit** | Resolved from the inner graph: **input** wires to the **entry** transformer (no inner predecessor); **output** from the **exit** node (no inner successor). Inner node ids are already in the body — authors do not repeat them in `ports`. |
| **`[port]`** | Optional on `input p1` / `output p2` when entry/exit has multiple ports (same default rules as flow links). |

Sugar `source -> example -> sink` when the `ports` block exposes exactly one input and one output.

Typecheck ([ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md)): stream types on `p1`/`p2` are taken from the wired inner ports.

**Studio extract** (C3) emits this `ports` block (names + `from` targets from the cut); no separate `boundary` keyword.

### IR

Resolved graph is still one `FlowGraph`, with two equivalent representations (tooling picks one; executor accepts both):

1. **Collapsed:** composite node (`FlowNodeKind.Composite`, `InnerFlowId`) + stored inner `FlowGraph`; external port list = `ports` block wires.
2. **Flattened:** inner nodes renamed to stable qualified ids (`example/one`, …) and edges rewired — **same executor** as today ([ADR-0083](DASHSPEC-ADR-0083-dataflow-engine-cockpit-transport.md)).

Compilation **must not** duplicate SQL sources when flattening; cache keys use logical `(flowNodeId, outputPort, filter snapshot)` from [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md).

### Where subprocesses live

- **Only** as **named** `flow <id> … end flow` (nested, included file, or Studio extract) inside the **module** graph.
- **Report / card** participate via **ports and `input`**, not via an extra default subprocess layer.

Card **diagram** is not a flow node. **`input from a.b`** is an edge in the report IR that resolves to `(nodeId, port)` on the module `FlowGraph`.

Studio **extract subprocess** = selection → saved `flow <id>` with the same inner body; parent edges rewritten to `… -> <id> -> …` using unwired port names from the cut.

### Non-goals (this ADR)

- New keyword `subflow` / `end subflow`.
- Separate runtime engine per nested flow.
- Automatic nesting of card `datasource` into inner flows (legacy compile-to-anonymous-source remains [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md) until migrated).

## Phased delivery

| Phase | Deliverable |
|-------|-------------|
| **C0** (this ADR) | Terminology, **explicit** boundary contract, parent sugar when 1× in/out |
| **C1** | Parse nested `flow <id>` (same body as `@flow`); IR composite node + inner graph |
| **C2** | `!include` + reference by id; flatten + typecheck |
| **C3** | Designer extract/collapse; **propose** boundaries on cut (author commits explicit text); executor subgraph preview |

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
  transformer one { ports input stream in: RawRow; output stream mid: MidRow end ports … }
  transformer two { ports input stream mid: MidRow; output stream out: RichRow end ports … }
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
enrich [out] -> publish

end flow pipeline
```

Equivalent parent sugar after boundary defaults exist:

```text
ingestion -> enrich -> publish
```

## Consequences

- Authors can name and reuse pipeline segments without copy-paste `datasource` or duplicate transformers.
- Card/report stay **views + consumers** on one module graph; subprocesses stay **explicit** (`flow id`) with inferred boundaries only when wrapping a cut.
- Studio gains **extract subprocess** (infer in/out) and **collapse** (`flow id` box).
- Parser and resolver gain a **composition** pass before `FlowGraph.typeCheck`; composite ports are derived from the inner graph, not a second keyword layer.
