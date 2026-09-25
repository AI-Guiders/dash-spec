# ADR-0056: Layout card groups (GroupBox)

**Status:** Accepted  
**Date:** 2026-09-25

## Context

Tab layout boards (`[ card … ]` rows) place cards on a CSS grid but carry no shared visual chrome. Repeated title prefixes (e.g. «Распределение запусков утилит по …») clutter dashboards. Catalog `group` (ADR-0030) groups report picker entries only; toolbar `group` (ADR-0037) groups filters — neither wraps dashboard cards.

## Decision

Introduce **`group`** blocks in tab/page layout boards (`.dashlayout` with `scope tab|page`, inline `layout board`).

```text
@layout luf_overview
scope tab

group launches_distribution {
  title = "Распределение запусков утилит"
  [ by_location by_project by_program by_user ]
}

[ by_form ]
[ by_hour ]
```

| Element | Role |
|---------|------|
| `group <id> { … }` | One outer grid row spanning full width |
| `title = "…"` | Optional group header (GroupBox label) |
| `[ … ]` inside group | Inner bracket rows (same rules as tab board) |
| Card `title` | Short label inside group («По локациям») |

**Not in scope:** nested groups, group-level filters, collapse, toolbar/card interior groups.

## Model

`LayoutBoardDefinition` holds `Entries`:

- `CardRow` — bracket row `[ a b c ]`
- `GroupRow` — `LayoutBoardGroupDefinition { Id, Title?, Rows }`

Backward-compat: `Rows` projects only top-level `CardRow` cells (toolbar/host unchanged).

## Placement

`TabLayoutPlanner` resolves:

- Top-level `CardRow` → main grid placements (unchanged)
- `GroupRow` → outer row `grid-column: 1 / -1`; inner board → nested grid placements

## Host

`Home.razor` renders `TabLayoutPlan` when a layout board exists; falls back to flat `VisibleCards()` when no board.

## Distinction

| Concept | ADR | Scope |
|---------|-----|-------|
| Catalog group | 0030 | Report picker sections |
| Toolbar filter group | 0037 | Filter bar |
| **Layout card group** | **0056** | Dashboard card chrome |

## Consequences

- LUF `overview.dashlayout` + shortened card titles
- Parser tests in `LayoutModuleParserTests.fs`, core tests in `LayoutBoardTests.cs`
