# DASHSPEC-ADR-0060: Vertical filter widget plugins

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-28 |
| **Relates to** | [ADR-0035](DASHSPEC-ADR-0035-chrome-and-filter-widget-families.md), [ADR-0059](DASHSPEC-ADR-0059-vertical-viz-plugins.md), [ADR-0022](DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md) |

## Context

Filter `widget = …` grammar and SQL/bind stay in Core (ADR-0035). Built-in widget UI lived in `DashSpec.Host` with `IFilterWidgetRenderer` pointing at host Razor types — unlike viz after ADR-0059.

Toolbar/card placement already uses `.dashlayout` with `scope toolbar|tab|card` (ADR-0021/0022/0026). Layout is not the gap; **widget presentation ownership** was.

## Decision

1. **`DashSpec.Filters`** — `FilterWidgetRenderContext` (host → plugin): filter definition, labels, options, selected values, date maps, callbacks. No SQL compile in plugins.
2. **`plugins/filter/DashSpec.Plugin.Filter.Builtins`** — RCL; `filter_widgets_builtin` registers `RegisterFilterWidgetComponent(widgetId, typeof(...))` for combobox, select, chips, day, range, top.
3. **Host** — `FilterWidgetHost` is a thin `DynamicComponent` slot (like `CardVisualization`). `FilterWidgetRegistry` resolves widget id → component type. Grid/placement remains host (`DashboardFiltersSection`, dashlayout).
4. **Execution unchanged** — filter state and queries stay in Core/Execution; viz still receives filtered `CardRenderResult`, not live toolbar state (unless a future narrow snapshot is added).

## Consequences

- New widget: add component under `plugins/filter/`, register id in plugin `RegisterContributors`.
- `FilterLargeListOptions` moved to `DashSpec.Presentation.Filters` (shared presentation helper).
- **Next:** product-specific filter widgets in external DLLs; optional `scope card` filter boards in `.dashlayout`.
