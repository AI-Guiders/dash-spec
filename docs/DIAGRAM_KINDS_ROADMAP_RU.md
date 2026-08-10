# DashSpec — какие типы диаграмм добавлять дальше

Заметка для авторов продукта (анализ на передышке).  
Цель: расширять **stdlib / registry** по реальному использованию дашбордов в мире, а не по «зоопарку красивых картинок».

Два трека:
- **LUF/LUS dogfood** — только то, что реально нужно продукту.
- **Standalone product** — chooser completeness для чужого автора дашборда (coverage + demo wow без sankey-кладбища).

Связано: [ADR-0003](../design/DASHSPEC-ADR-0003-diagram-kinds-registry.md) (registry kinds), [ADR-0017](../design/DASHSPEC-ADR-0017-file-includes-and-stdlib.md) (stdlib presentations).

---

## 1. Что уже есть в движке (registry)

| Kind | Family | В demo / stdlib chrome |
|------|--------|-------------------------|
| `bar` | Chart | demo + H/V + **stacked** (`bar_stacked_360`) |
| `line` | Chart | demo + line presentations |
| `area` | Chart | demo + `area_bottom_320` (`fill = area`) |
| `sparkline` | Chart | demo + `sparkline_64` |
| `pie` / `donut` / `doughnut` | Chart | demo donut + `donut_right_360`, `pie_right_360` |
| `scatter` | Chart | demo + `scatter_360` (+ optional `size` → bubble) |
| `histogram` | Chart | demo + `histogram_320` (bins in Core) |
| `box` / `boxplot` | Chart | demo + `box_360` (Chart.js boxplot plugin) |
| `treemap` | Chart | demo + `treemap_360` (canvas layout) |
| `gauge` | Chart | demo + `gauge_200` (doughnut semicircle) |
| `table` | Table | demo |
| `heatmap` | Matrix | demo + `heatmap_tall` |
| `number` | Scalar | demo KPI + `kpi_compact` + `delta = prior` |

LUF догфуд: donut + horizontal/vertical bar — пресеты chrome в **stdlib**.

---

## 2. Что говорят мировые дашборды

### Tableau Public census (~25 600 дашбордов, IEEE VIS 2025)

Источник: [From a Dashboard Zoo to Census](https://www.tableau.com/blog/tableau-research-understanding-dashboard-design-at-scale) (Tableau Research).

| Находка | Цифра | Следствие для DashSpec |
|---------|-------|-------------------------|
| Charts ≈ половина блоков | ~50% | kinds + chrome — ядро продукта |
| Text как блок | ~21% | позже: text/markdown card (не chart kind) |
| **Bar** | в **60%** дашбордов | уже закрыто; держать H/V + stacked |
| **Line** | ~**25%** | уже есть; area / multi-series polish |
| **Maps** | ~**25%** | отдельное семейство geo — дорого, не v0.x mid |
| Sankey / waterfall / «bespoke» | **&lt;0.5%** | не приоритет stdlib |
| Interact: filter widgets → charts | 69% | у нас toolbar + bind уже |
| Legend → chart filter | 43% | donut legend→filter уже dogfood |

Вывод census: **простота побеждает**.

### Практические chooser’ы (Power BI / Basedash / InetSoft / ClicData)

| Вопрос | Типичный chart | У нас |
|--------|----------------|-------|
| Сравнить категории | bar / column (H или V) | ✅ |
| Тренд во времени | line (иногда area) | ✅ line + area |
| Одно число / статус | KPI card / big number | ✅ `number` |
| KPI vs цель / диапазон | gauge / bullet | ✅ `gauge` |
| Часть от целого (≤5) | donut / pie | ✅ |
| Часть от целого (много) | stacked bar / treemap | ✅ stacked + `treemap` |
| Две оси × значение | heatmap / matrix | ✅ |
| Точные значения | table | ✅ |
| Связь двух мер | scatter / bubble | ✅ scatter + optional `size` → bubble |
| Распределение | histogram / box | ✅ histogram + `box` |
| Компактный тренд | sparkline | ✅ |
| Воронка / поток | funnel / sankey | нет (редки в census) |
| Гео | map | нет |

---

## 3. Приоритеты

### P0 — ✅ закрыто

1. **KPI / `number`** — demo + `kpi_compact` + scalar rollup.
2. **Stacked bar** — `series` в SELECT для bar + demo + `bar_stacked_360`.
3. **Area** — kind `area` / `fill = area` + demo + `area_bottom_320`.

### P1 — ✅ закрыто (ядро)

| Kind | Ship |
|------|------|
| **Scatter** | kind + Chart.js scatter/bubble (`size`) + demo idle×peak |
| **Histogram** | Core binning + demo idle_minutes |
| **Sparkline** | kind + compact chrome + demo |
| **KPI delta** | `delta = prior` vs equal-length prior period |

Опциональный polish: histogram `bin_width` UX.

### Standalone product track — ✅ batch shipped

Chooser pack (не LUF-driven):

| Kind | Ship |
|------|------|
| **Box / boxplot** | Core group samples + Chart.js boxplot CDN + demo by app |
| **Treemap** | Core tiles + canvas squarify + demo peak by app |
| **Gauge** | scalar rollup + doughnut semicircle + demo peak |

Дальше по standalone (не сейчас): waterfall / funnel как чеклист-parity; sunburst после реального hierarchy dogfood.

### P2 — полезно, но нишево или дорого

| Kind | Заметка |
|------|----------|
| **Waterfall** | finance / bridge; &lt;0.5% Tableau census |
| **Funnel** | sales/ops |
| **Map** | отдельный эпик |
| **Sankey / network** | census почти не использует |
| **Sunburst** | после treemap, тот же смысл |

### Не в stdlib (пока)

- 3D, radar/spider «для красоты», dual-axis как default.
- Продуктовые LUF/LUS diagram **id** — остаются в продукте; в Core только нейтральные chrome presets и demo на `demo.v_*`.

---

## 4. Stdlib сейчас (presentation chrome)

Путь: `src/DashSpec.Core/stdlib/presentation/` → include `<presentation/name>` или `chrome { use name }`.

| Preset | Назначение |
|--------|------------|
| `bar_bottom_320` | bar, легенда снизу, h=320 |
| `bar_horizontal_360` | bar H chrome |
| `bar_vertical_360` | bar V chrome |
| `bar_stacked_360` | stacked bar, h=360 |
| `donut_right_360` | donut, легенда справа |
| `pie_right_360` | pie, легенда справа |
| `area_bottom_320` | area fill, h=320 |
| `kpi_compact` | number KPI, h=120 |
| `sparkline_64` | sparkline, h=64 |
| `scatter_360` | scatter, h=360 |
| `histogram_320` | histogram bar, h=320 |
| `box_360` | boxplot, h=360 |
| `treemap_360` | treemap, h=360 |
| `gauge_200` | gauge, h=200 |
| `heatmap_tall` | heatmap chrome |

---

## 5–6. Источники

- Tableau Research — [Dashboard design census](https://www.tableau.com/blog/tableau-research-understanding-dashboard-design-at-scale).
- Power BI practical set — [The Data Alchemist](https://thedataalchemist.co/data-insights/power-bi-charts/).
- Chart choosers — [Basedash](https://www.basedash.com/blog/how-to-choose-the-right-chart-for-a-dashboard), [ClicData](https://www.clicdata.com/blog/a-chart-chooser-for-bi-teams-stop-guessing-start-deciding/), [InetSoft tree](https://www.inetsoft.com/company/dashboard_visualization_tree/).

---

## 7. Следующий конкретный leaf

1. Histogram `bin_width` UX polish (если нужен dogfood).
2. Waterfall / funnel — только если standalone checklist реально болит.
3. Map / sankey — не раньше отдельного эпика.
