# DASHSPEC-ADR-0063: Layout board nest (cell as grid)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-28 |
| **Relates to** | [ADR-0056](DASHSPEC-ADR-0056-layout-card-groups.md), [ADR-0061](DASHSPEC-ADR-0061-layout-board-weights-and-filter-place.md), [ADR-0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md) |

## Context

Tab boards place cards on a shared column grid. **Groups** (ADR-0056) add an inner grid but only as a **full-width** row with optional GroupBox chrome. Authors need a **nested board inside one cell** of a bracket row (same placer, weights, and card refs) without new card/filter mechanics.

## Decision

### Declare `nest`

Named inner board on the tab/page layout (does not consume an outer grid row):

```text
nest analytics_strip {
  [ Q E ]
  [ T ]
}

[ wide_card analytics_strip:2 narrow_card ]
```

| Element | Role |
|---------|------|
| `nest <id> { … }` | Inner bracket rows only (same as group inner rows) |
| Token `<id>` in `[ … ]` | Occupies one weighted cell; renders nested grid |
| `:weight` | ADR-0061 — share of outer row width |

Resolution: layout token matches **nest id** before card ref/id. Duplicate ids or card/nest id clash → parse/lint error.

### vs `group`

| | `group` | `nest` |
|---|---------|--------|
| Outer row | Full width (`1 / -1`) | One cell in a `CardRow` |
| Chrome | Optional `title` | None (layout container only) |
| Nesting | No nested groups (0056) | No nested `nest` in v1 |

### Planner / Host

- `TabLayoutPlanner` resolves nest outer `PlacementDefinition` + inner placements via `LayoutBoardPlacer`.
- Host renders `.card-nest` at outer placement; children use the same `PlacementGridStyle` as tab cards.

### Scope

- Tab and page layout boards (`.dashlayout`, inline `layout board`).
- Not toolbar, not card interior board, not catalog/toolbar groups.

## Non-goals (v1)

- `nest` inside `nest` or inside `group`
- Per-nest `columns` (inherits dashboard `layout grid`)
- `title` on nest (use `group` when chrome is needed)

## Consequences

- `LayoutBoardNestRow`, `LayoutNestPlacement`, `LayoutBoardRefResolver`
- `TabLayoutPlan.Nests` for Host
- Parser keyword `nest`; F# `LayoutBoardEntry.NestRow`
