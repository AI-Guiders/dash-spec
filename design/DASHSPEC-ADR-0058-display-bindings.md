# DASHSPEC-ADR-0058: Display bindings (`bind display`)

**Status:** Accepted  
**Date:** 2026-09-25  
**Relates to:** [ADR-0057](DASHSPEC-ADR-0057-resolution-registry.md), [ADR-0029](DASHSPEC-ADR-0029-inspect-tooltip-presentation-split.md)

## Context

Layout group and card titles are static strings. Toolbar filters (`chart_top`, `period_start`, …) already drive SQL and chips, but authors cannot reference their **display values** in titles — e.g. group «Топ-10» should become «Топ 25» when the top filter changes.

Tooltip `variables` solve row interpolation; legend `{min}` / `{max}` solve matrix bounds. Display titles need a third source: **filter UI state**.

## Decision

### 1. `bind display` on page (v2)

```text
page executive_summary
  toolbar period_grain, period_start, chart_top

  bind display
    top = chart_top.value
    range = period_start.range
    grain = period_grain.label
  end bind

  include layout "layouts/stakeholder-page-executive.dashlayout"
  ...
end page
```

| Element | Role |
|---------|------|
| `bind display` | Declares display slots for the page |
| `slot = source` | Slot name → `filter` or `filter.property` |
| `{slot}` in `title` | Resolved at render from toolbar filter state |

### 2. Source paths

| Filter kind | Property | Display |
|-------------|----------|---------|
| `top` | `value` (default) | current top N; `0` when `min = 0` → `все` |
| `top` | `label` | filter label |
| `date` | `range` (default) | chip range per grain |
| `date` | `value`, `from`, `to`, `grain`, `label` | formatted parts |
| `field` | `chip` (default) | selected values or `все` |
| `field` | `label`, `value` | label / first value |

Omitted property uses the default for that filter kind.

### 3. Usage surfaces (v1 ship)

- `layout group { title = "Топ {top}" }`
- `card { title = "… {range} …" }` (chrome title after ADR-0057 merge)

Fallback: `{filter_name}` without `bind display` resolves the filter directly with default property.

### 4. Runtime

- `DisplayTemplate` — `{slot}` parser (shared pattern with tooltip/legend)
- `FilterDisplaySource` — `filter.property` → string from `FilterDisplayContext`
- `DisplayTitleResolver` — merge bindings + template
- Host resolves on every render (groups) and card enrich (titles)

### 5. Non-goals (v1)

- Report-level `bind display` (page only)
- Nested templates inside `bind display` RHS (`caption = "{grain}: {range}"`)
- SQL row / diagram data in display bindings

## Consequences

- Executive summary: `title = "Топ {top}"` tracks `chart_top`
- Validate: unknown filter in source path → diagnostic (follow-up)
- ADR-0057: add `display.bind` as optional chain source for `layout_group.chrome_title` / `card.chrome_title`
