# DASHSPEC-ADR-0065: Card interior multi-slot (no legacy host stack)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-29 |
| **Supersedes** | [ADR-0025](DASHSPEC-ADR-0025-card-interior-layout-board.md) §«Без layout» |
| **Relates to** | [ADR-0063](DASHSPEC-ADR-0063-layout-board-nest.md), [ADR-0064](DASHSPEC-ADR-0064-cell-drill-tabular-payload.md), [ADR-0038](DASHSPEC-ADR-0038-structured-card-and-report-composition.md) |

## Context

Tab boards and `nest` already treat a cell as an inner bracket grid. Card interior v1 allowed only **one** diagram slot plus local filters; drill shipped as a **second card**. Host kept a **legacy** path: cards without `layout { }` rendered filters + diagram as a fixed stack.

Authors need **N diagram slots** inside one card (heatmap + drill table), same bracket syntax as tab/nest, without a second card chrome.

## Decision

### 1. Diagram slots on card

| Concept | Rule |
|---------|------|
| **Slot id** | `diagram ref H` → token `H` in interior board; anonymous single diagram → reserved token `diagram` (`__diagram__` in IR) |
| **Data** | `data` → primary slot; `data for drill` → slot `drill` |
| **Per slot** | `Diagram`, `DataSource`, `bind` list (slot-local; may match siblings) |
| **Card-level** | `on click`, `views`, `limits` (primary matrix), title, chrome — still on card |
| **Primary slot** | First declared slot, or slot named by `views` / `diagram ref` used as default view target |

### 2. Interior board required (compiler default)

Every card must resolve to an **interior board** before Host runs.

- Author may omit `layout { }` → parser **synthesizes** a default board (single row `[ diagram ]`, or filter row(s) + `[ diagram ]`) — same placer, **no** alternate Host markup path.
- Author **must** list every diagram slot ref and every local filter token when using explicit `layout { }`.

Host renders **only** `.card-interior-grid` from `InteriorPlacements` (filters and diagrams in grid cells).

### 3. Rendering

- **Primary** slot payload remains on `CardRenderResult` top-level fields (toolbar, views, export).
- **Additional** slots: `InteriorSlotRenders[slotId]` with same viz families as today (table, matrix, chart, …).
- Click / heatmap navigation uses **primary** slot. Interior drill after `drill table from cell` uses per-card overlay + **CardInteriorSlots** refresh ([ADR-0066](DASHSPEC-ADR-0066-card-refresh-scopes.md)); heatmap stays on dashboard filter scope.

### 4. LUS pilot

`peak_concurrent_proxy`: merge `peak_concurrent_proxy_drill` into one card:

```text
diagram ref main …
diagram ref drill lus_peak_concurrent_proxy_drill_table
data … end data
data for drill … end data
layout
  [ main ]
  [ drill ]
end layout
```

Remove drill card from overview layout.

## Composition contract (host grid, plugin fill)

**Host** only materializes **bracket grids** and passes **named cells** to renderers:

| Grid | Who declares tokens | Who renders into a cell |
|------|---------------------|-------------------------|
| Tab / page board | `.dashlayout` | Card shell (one cell = one card chrome + interior root) |
| Card `filters layout` | Spec weights (`usage_date:2 apply:1 …`) | Filter plugin widgets + manual Apply ([ADR-0060](DASHSPEC-ADR-0060-vertical-filter-plugins.md)) |
| Card `layout { }` interior | Spec rows (`[ heatmap ]`, `[ drill ]`) | Viz plugin per `diagram ref` / `InteriorSlotRenders` |

**Spec / Core** answer: «это карточка, у меня N слотов» (boards + binds + click) and «это диаграмма, я в слот `drill`» (`data for`, `diagram ref`).

**Plugins** answer: «я занимаю слот X» — filter widget id or viz renderer id; no filter/diagram semantics in Host beyond iterating placements and wiring session callbacks.

Target: Host keeps card chrome shell (title, fold, export, fullscreen, extension blocks) and iterates interior placements; filter chrome (`CardLocalFilterChrome`, `FilterWidgetHost`) lives in `DashSpec.Plugin.Filter.Builtins`; viz dispatch (`CardVizHost`, `CardInteriorVizHost`) in `DashSpec.Plugin.Viz.Builtins`; shared slot order/CSS in `DashSpec.Core` (`CardFilterChromeSlotOrder`, `PlacementGridCss`) and `CardInteriorGrid` in Presentation.

## Non-goals (this ADR)

- `nest` inside card
- Per-slot `views` combobox
- `toggle_viz_legend` (follow-up with ADR-0062)
- Replacing `CardDefinition` with anonymous layout nodes (card stays the bind/click/chrome unit)

## Consequences

- Parser: `DiagramSlots` map, `data for`, multiple `diagram ref`, default interior board
- Core: `CardInteriorLayoutCompactor` validates all slot ids
- Host: multi-cell interior grid; remove legacy `card-viz-stack` without interior
- Tests + LUS dashspec migration
