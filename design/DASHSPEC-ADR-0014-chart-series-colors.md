# DASHSPEC-ADR-0014: Chart series colors in spec

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-06-29 |
| **Relates to** | [ADR-0003](DASHSPEC-ADR-0003-diagram-kinds-registry.md), [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md), [ADR-0008](DASHSPEC-ADR-0008-viz-render-plugins.md) |

## Context

Heatmap уже поддерживает `color_scale` из diagram ([ADR-0004](DASHSPEC-ADR-0004-diagram-column-as.md)). Line/bar брали фиксированную палитру из `charts.js` — продуктовые цвета (LUS app_name) нельзя было задать в dashspec.

## Decision

### `@diagramlibrary` — секция `[palette.<id>]`

```toml
[palette.lus_apps]
colors = "#3b82f6,#22c55e,#eab308"
default = "#94a3b8"
Tekla = "#e11d48"
Other = "#94a3b8"
```

| Ключ | Смысл |
|------|--------|
| `colors` | упорядоченный fallback через запятую (стабильный hash по имени серии, не по порядку в data) |
| `default` | цвет для неизвестных серий и `Other` |
| *остальные* | `series` value → hex (ключ = имя серии; prefix-match: `Cursor` → `Cursor IDE`) |

### Diagram / presentation

```toml
[diagram.lus_activity_5min_line]
color_palette = "lus_apps"
series_colors = "LegacyApp:#64748b"
```

| Свойство | Где | Смысл |
|----------|-----|--------|
| `color_palette` | diagram, presentation | ссылка на `[palette.*]` |
| `colors` | diagram, presentation | inline ordered list |
| `series_colors` | diagram, presentation | `Name:#hex, Name2:#hex` |

Порядок слияния (слабое → сильное): **diagram** → **presentation preset** → **presentation inline**.

### Runtime

- `ChartColorResolver` → `ChartSeries.Color`
- Host `charts.js` использует `series[].color`, иначе fallback-палитру

Bar и line — одинаково. Matrix по-прежнему `color_scale`.

## Non-goals

- per-point gradients
- автоподбор контраста (a11y) — позже
- dashboard-level global palette (все диаграммы делят `[palette.*]` из library; fallback по **имени серии**, не по порядку в ответе SQL)

## Consequences

- LUS: `[palette.lus_apps]` в `lus-diagram-library.toml`
- Demo: `[palette.demo_apps]`
- Новые продукты — строка в palette TOML, без правки Host
