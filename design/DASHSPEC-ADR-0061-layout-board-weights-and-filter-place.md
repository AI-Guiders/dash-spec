# DASHSPEC-ADR-0061: Layout board weights and filter `place`

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-28 |
| **Relates to** | [ADR-0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md), [ADR-0022](DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md), [ADR-0002](DASHSPEC-ADR-0002-layout-and-presentation.md) |

## Context

Bracket layout rows divide width equally (`columns / N`). Toolbar and tab boards need unequal columns (e.g. narrow date + wide combobox). Cards already support explicit `place { row col span }` overriding the board; filters did not (ADR-0022 non-goal).

## Decision

### Weighted board cells

In `[ … ]` rows, a cell may be `ref` or `filter` id with optional **`:weight`** (positive integer, default 1):

```text
toolbar
[ D:1 P:3 ]
end toolbar
```

On `columns = 12`, weights `1` and `3` → spans **3** and **9**. Single-cell rows stay full width. Unweighted cells behave as today (equal split).

Lexer adds `:` as a token inside board rows only (parser).

### Filter `place`

Same grammar as card `place` (ADR-0002): `row`, `col`, `span` (`full` / `half` / `third` or integer).

```text
filter field products … ref P
place { row = 1 col = 4 span = 9 }
```

Or inside structured filter:

```text
filter products {
  bind field …
  show ref = P widget combobox
  place { row = 1 col = 4 span = 9 }
}
end filter
```

**Precedence:** explicit `place` on filter overrides toolbar/tab board placement for that filter (mirrors cards).

### Scope

- Toolbar, tab/page boards, group inner rows — same weight algorithm.
- Card `place` unchanged; board weights apply to cards too.

## Non-goals

- Ratios tied to viewport breakpoints (`place @sm`)
- Weights not summing to `columns` (fractional grid uses integer spans + last cell absorbs remainder)

## Consequences

- `LayoutBoardPlacer` centralizes weighted row placement; `TabLayoutPlanner` and `ToolbarLayoutCompactor` call it.
- `FilterDefinition.Placement` in Core + F# parse model.
