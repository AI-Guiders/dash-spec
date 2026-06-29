# DASHSPEC-ADR-0008: Diagram library presets and viz render plugins

| | |
|---|---|
| **Status** | Accepted · diagram presets **implemented**; `render` plugins **proposed** |
| **Date** | 2026-06-29 |
| **Relates to** | [ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md), [ADR-0003](DASHSPEC-ADR-0003-diagram-kinds-registry.md), [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md) |

## Context

- `diagram <kind> { … }` задаёт привязку данных (registry + `DataFamily`).
- [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md) вынес `presentation` и `transform series` в отдельные блоки и пресеты `presentation.*` / `transform.series.*`.
- На практике типовые карточки в sample повторяют один и тот же «рецепт»: kind + bindings + render + presentation + transform.
- Host сейчас **жёстко** рендерит: line/bar → Chart.js, heatmap → CSS grid.
- Inline `code = ...` в spec — нежелателен (безопасность, версии).

## Decision

### Именованный рецепт в `@diagramlibrary`

При подключённой библиотеке карточка ссылается на **полный пресет диаграммы**:

```text
card peak as "Peak" {
  diagram demo_peak_concurrent_line
  datasource view demo.v_daily_peak_concurrent_proxy
}
```

Опциональный override полей (merge поверх пресета):

```text
diagram demo_peak_concurrent_line { y = other_metric }
```

TOML библиотеки:

```toml
[diagram.demo_peak_concurrent_line]
kind = "line"
render = "chartjs"
presentation = "line_bottom_300"
transform.series = "top5"
x = usage_date
y = peak_concurrent_proxy
series = app_name
```

| Слой | Где живёт |
|------|-----------|
| **kind + bindings** (`x`, `y`, `series`, `columns`, …) | `[diagram.<id>]` + override на card |
| **`render`** | `[diagram.<id>]` (plugin id; host default пока игнорирует) |
| **`presentation` / `transform.series`** | ссылки на `presentation.*` / `transform.series.*` в том же TOML |
| **datasource / bind / legend card** | только на card |

Порядок слияния на card:

1. `[diagram.<id>]` из library
2. inline в `diagram <id> { … }` на card
3. `presentation` / `transform series` на card (override ссылок из пресета)
4. legacy-свойства в `diagram` (deprecated, ADR-0007)

### Явный kind — по-прежнему допустим

```text
diagram line { x = … y = … }
presentation { use = line_bottom_300 }
transform series { use = top5 }
```

Для разовых карточек или когда библиотека не подключена.

### Viz-plugins (`render`) — следующий инкремент

`render` в `[diagram.<id>]` резервирует plugin id из manifest:

```toml
[[viz.load]]
id = "chartjs"
assembly = "DashSpec.Viz.ChartJs.dll"

[[viz.load]]
id = "css-grid"
assembly = "DashSpec.Viz.CssGrid.dll"
```

До появления `IVizPlugin` host использует текущие встроенные рендереры по `DataFamily`; поле `render` парсится и хранится, но не переключает backend.

## Non-goals

- `code = "..."` в `.dashspec`
- Hot reload viz bundle
- Глобальные `diagram` defaults на уровне dashboard

## Consequences

- **Implemented:** `SpecLibrary` `[diagram.*]`, `CardDiagramResolver`, `samples/demo/demo-diagram-library.toml` + `demo-soak.dashspec`.
- **Next:** `IVizPlugin` + honor `render` из resolved preset.
- Пресеты `presentation.*` / `transform.series.*` остаются переиспользуемыми building blocks внутри `[diagram.*]` и для явных card-блоков.
