# DashSpec stdlib

Встроенные фрагменты ([ADR-0017](../../../design/DASHSPEC-ADR-0017-file-includes-and-stdlib.md)).

Include: `!include "<presentation/donut_right_360>"` или на diagram:

```text
chrome
  use donut_right_360
end chrome
```

## presentation/

| Id | Notes |
|----|--------|
| `bar_bottom_320` | legend bottom, height 320 |
| `bar_horizontal_360` | chrome for horizontal bars |
| `bar_vertical_360` | chrome for vertical bars |
| `bar_stacked_360` | stacked = true, height 360 |
| `donut_right_360` | legend right, height 360 |
| `pie_right_360` | legend right, height 360 |
| `area_bottom_320` | fill = area, height 320 |
| `kpi_compact` | number KPI, height 120 |
| `sparkline_64` | compact line, height 64 |
| `scatter_360` | scatter chrome, height 360 |
| `histogram_320` | histogram bar, height 320 |
| `heatmap_tall` | matrix chrome |

Roadmap kinds: [docs/DIAGRAM_KINDS_ROADMAP_RU.md](../../../docs/DIAGRAM_KINDS_ROADMAP_RU.md).
