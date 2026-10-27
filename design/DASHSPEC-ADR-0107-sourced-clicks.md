# DASHSPEC-ADR-0107: Sourced clicks — `on <source> click` + the click table

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-20 |
| **Relates to** | [ADR-0106](DASHSPEC-ADR-0106-multi-form-constructs.md) (construct declaration), [ADR-0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md) (plugin vocabulary) |

## Context

Today a card has one `on click` block with an implicit source: matrix cell clicks and
Chart.js category clicks both flow into the same callback (`OnHeatmapCellSelected`),
and the drill effect carries the source (`drill table from cell`). The clickable
surface vocabulary belongs to the diagram plugin — a matrix has cells and axis
category items; other kinds have their own surfaces. The platform should not know
what a heatmap's axes are.

First consumer: the Activity-per-day card — clicking an application on the Y axis must
open a (user, launches) table for that application, view like the current cell drill.

## Decision

### Source-explicit handlers, two forms (ADR-0106 declaration)

```text
on click
  | source        | drill     | set                          |
  | cell          | drill     | activity_hour=x, app_name=y |
  | category item | app_drill | app_name=y                   |
end click
```

desugars to:

```text
on cell click
  drill table drill
  set activity_hour from x
  set app_name from y
end click

on category item click
  drill table app_drill
  set app_name from y
end click
```

- Vocabulary: `source` (required, unique per row) — validated against the
  **plugin-declared click-source registry** (matrix renders: `cell`, `category item`);
  `drill` — the target slot ref, `-`/empty = a set-only click (no drill); `set` — the
  bind mini-list `filter=field, …` with the established field words `x|y|value`.
- The drill effect simplifies: the source moved to the header — `drill table <slot>`;
  unnamed slot keeps the legacy apply-to-all-drill-slots behavior.
- Legacy `on click` (source-less block) stays the **generic fallback**: the dispatcher
  tries the source-specific handler first, then the generic. No existing spec breaks.

### Event module boundary

- **Registry**: click sources are plugin-declared and validated at parse (the same
  channel as `KnownActionHandlers`).
- **Contexts**: `DashSpec.Core.Interaction` (already home of `HeatmapCellContext`) gains
  the category-item context — axis, index, label. A category-item click satisfies
  `set … from y` (Y-axis items); `from x` stays unset for it.
- **Dispatch**: source → the card's handler for that source, with the generic fallback.

### Drill slot targeting

- The cell-drill overlay gains a target slot ref; a targeted overlay applies only to
  its slot, an untargeted overlay applies to all interior drill slots (today's
  behavior — backward compatible).

### Renderer (matrix)

- Y-axis labels are already DOM (`.matrix-canvas-y-label`); they become clickable when
  the card declares a `category item` handler; the click emits the category-item event
  (mirrors the existing Chart.js `OnCategoryClick`).

## Consequences

- The Activity card gets the app drill: users × launches per application for the
  selected day (the dashboard view + flow; the drill table chrome).
- `when` gets both forms free via ADR-0106.
- Chart.js category clicks migrate onto the same registry later — the seam's second
  instance.
