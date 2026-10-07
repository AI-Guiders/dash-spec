# DASHSPEC-ADR-0093: Qualified flow graph kinds

| | |
|---|---|
| **Status** | Accepted · Implemented |
| **Date** | 2026-10-07 |
| **Relates to** | [ADR-0091](DASHSPEC-ADR-0091-unified-graph-flow-and-routing.md), [ADR-0092](DASHSPEC-ADR-0092-report-scope-routing-and-events.md), [ADR-0090](DASHSPEC-ADR-0090-card-interior-flow.md) |

## Context

A single `flow … end flow` block mixed **data**, **show** (filter chrome), **wire** (host routes), and **action** edges. Authors and linters could not tell intent from ports alone without reading every line.

## Decision

### Same edge grammar, four qualified containers

Link lines are unchanged (`producer [out] -> [in] consumer`). Containers:

| Keyword block | Role | Typical scope |
|---------------|------|----------------|
| `data flow` | Module rows + filter → diagram slot | card |
| `show flow` | Filter → toolbar chrome (`report` / `page` / `card`) | report, page, card |
| `wire flow` | Host browse → child card | page, card |
| `action flow` | Event edges in flow form (v1 often empty; `on click` remains) | page, card |

Syntax:

```text
show flow
  usage_date -> [toolbar] chrome.dashboard
end show flow
```

Bare `flow` is a **parse error** (no legacy container).

### Extensibility

Kinds are declared in `FlowGraphKindRegistry` (`DashSpec.Modeling.Core`). Adding or renaming a kind is a registry + validation update; edge shape stays the same.

### Parse-time lint

`FlowGraphLinkRules` infers each link’s role and rejects lines in the wrong qualified block.

### IR

`ScopeFlowDefinition` and `CardInteriorFlowDefinition` store `FlowGraphSections` (map `FlowGraphKind` → links). Applicators and wiring read **show / wire / action** vs **data** from sections, not from mixed lists.

## Consequences

- Specs must use qualified blocks (migration script: `scripts/split-qualified-flow-blocks.py`).
- Studio can render four layers from `FlowGraphKindRegistry.allKinds`.
- Author-facing `report` / `page.*` / `card.*` anchors remain a follow-up to replace `chrome.*` targets.
