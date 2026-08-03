# DASHSPEC-ADR-0036: End blocks, page toolbar, filter derive, presentation scale

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-07-08 |
| **Relates to** | [ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md), [ADR-0026](DASHSPEC-ADR-0026-layout-module-scope.md), [ADR-0022](DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md) |

## Context

Stakeholder UX exposed DSL gaps: toolbar filters visible on pages that do not bind them, nested `{}` hard to read, utilization chart scale/colors wrong, duplicate filter chips on cards.

## Decision

### 1. Block syntax: `end <kind>` (required for containers)

Containers close with labeled `end`, not bare `end`:

```text
page peak_util
  card peak_by_app
    title = "…"
    …
  end card
end page
```

Legacy `{ … }` remains valid. Optional id: `end card peak_by_app`.

Kinds: `tab`, `dashboard`, `report`, `standalone`, `filters`, `page`, `phase`, `card`, `click` (`on click` … `end click`), `chrome`, `runtime`, `configuration`, `wiring`, `chrome` (toolbar chrome).

### 2. Page toolbar

```text
page peak_util
  toolbar period_grain, period_start, app_name
```

Host shows only filters listed on the active page (intersected with card bindings).

### 3. Filter derive

```text
page multi_app
  derive usage_date from period_start grain period_grain
```

On page enter, Host sets `usage_date` to period bounds from `period_start` + grain.

### 4. Card chrome

```text
card x
  chrome
    bound_filters = hidden | toolbar_only | chips
  end chrome
```

Default `chips`. `hidden` / `toolbar_only` suppress scope chips when filter is in toolbar.

### 5. Presentation

`.dashpresentation` / diagram: `y_max`, `color_mode = single`, `default`, `colors`.

## Consequences

- Core: `BlockSyntax`, `PageToolbarResolver`, `FilterDeriveDefinition`, `CardChromeDefinition`
- Host: page-scoped toolbar, derive sync on `SelectPage`, chart `valueAxisMax`
- LUS: `lus-dev-stakeholder.dashspec` migrated to end syntax (pilot)
