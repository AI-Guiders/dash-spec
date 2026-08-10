# DashSpec — какие типы диаграмм добавлять дальше

Заметка для авторов продукта (анализ на передышке).  
Цель: расширять **stdlib / registry** по реальному использованию дашбордов в мире, а не по «зоопарку красивых картинок».

Связано: [ADR-0003](../design/DASHSPEC-ADR-0003-diagram-kinds-registry.md) (registry kinds), [ADR-0017](../design/DASHSPEC-ADR-0017-file-includes-and-stdlib.md) (stdlib presentations).

---

## 1. Что уже есть в движке (registry)

| Kind | Family | В demo / stdlib chrome |
|------|--------|-------------------------|
| `bar` | Chart | demo + `bar_bottom_320`, `bar_horizontal_360`, `bar_vertical_360` |
| `line` | Chart | demo + line presentations в sample |
| `pie` / `donut` / `doughnut` | Chart | demo donut + `donut_right_360`, `pie_right_360` |
| `table` | Table | demo |
| `heatmap` | Matrix | demo + `heatmap_tall` |
| `number` | Scalar | **kind есть, demo/stdlib KPI chrome — нет** |

LUF догфуд: donut + horizontal/vertical bar — пресеты chrome теперь в **stdlib**, чтобы не тащить копии в каждый продукт.

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

Вывод census: **простота побеждает**. Расширять kinds имеет смысл там, где вопрос бизнеса не закрыт bar/line/table/KPI, а не ради «ещё один тип в палитре».

### Практические chooser’ы (Power BI / Basedash / InetSoft / ClicData)

Повторяющийся каркас «вопрос → визуал»:

| Вопрос | Типичный chart | У нас |
|--------|----------------|-------|
| Сравнить категории | bar / column (H или V) | ✅ |
| Тренд во времени | line (иногда area) | ✅ line; area — нет |
| Одно число / статус | **KPI card / big number** | kind `number` есть, UI sample нет |
| Часть от целого (≤5) | donut / pie | ✅ |
| Часть от целого (много / иерархия) | stacked bar, treemap | stacked prop есть; treemap — нет |
| Две оси × значение | heatmap / matrix | ✅ |
| Точные значения / много атрибутов | table / matrix table | ✅ table |
| Связь двух мер | scatter / bubble | нет |
| Распределение | histogram / box | нет |
| Воронка / поток | funnel / sankey | нет (и редки в census) |
| Гео | map / choropleth | нет |

Консенсус гайдов: pie/donut только для **2–5** категорий; для рангов — sorted horizontal bar; не плодить типы на одном экране.

---

## 3. Приоритеты для DashSpec (предложение)

### P0 — закрыть пробелы уже существующих kinds

1. **KPI / `number`** — demo-card + stdlib chrome (крупная цифра, опционально delta). Самый частый «визуал» после bar/line в operational BI, у нас kind уже в registry.
2. **Stacked bar** — убедиться, что `stacked` + series в demo/stdlib задокументированы (prop уже в chrome). Composition без pie.
3. **Area (line family)** — либо `diagram area`, либо `line` + `fill = area`. Закрывает «объём во времени» без нового семейства.

### P1 — высокий ROI, всё ещё «простые»

| Kind / capability | Зачем | Сложность |
|-------------------|-------|-----------|
| **Scatter** (+ optional bubble size) | correlation / outliers (license vs usage, cost vs peak) | Chart family, Chart.js scatter |
| **Histogram** (или bar + bin transform) | распределение длительностей / idle | Chart + transform в Core |
| **Sparkline** (compact line) | KPI row / table cell trend | presentation + small height |

### P2 — полезно, но нишево или дорого

| Kind | Заметка |
|------|---------|
| **Treemap** | иерархия part-to-whole; нужен layout engine ≠ Chart.js default |
| **Waterfall** | finance / bridge; &lt;0.5% Tableau census |
| **Funnel** | sales/ops; чаще маркетинг, чем license soak |
| **Gauge / bullet** | KPI status; часто заменяемо number + color |
| **Map** | 25% Tableau Public, но другая data family + tiles + CRS — отдельный эпик |
| **Sankey / network** | census почти не использует |

### Не в stdlib (пока)

- 3D, radar/spider «для красоты», dual-axis как default (гайды предостерегают).
- Продуктовые LUF/LUS diagram **id** (`luf_by_*`) — остаются в продукте; в Core только **нейтральные** chrome presets и demo на `demo.v_*`.

---

## 4. Stdlib сейчас (presentation chrome)

Путь: `src/DashSpec.Core/stdlib/presentation/` → include `<presentation/name>` или `chrome { use name }`.

| Preset | Назначение |
|--------|------------|
| `bar_bottom_320` | bar, легенда снизу, h=320 |
| `bar_horizontal_360` | bar H chrome (orientation задаётся в diagram) |
| `bar_vertical_360` | bar V chrome |
| `donut_right_360` | donut, легенда справа |
| `pie_right_360` | pie, легенда справа |
| `heatmap_tall` | matrix |

Demo: `samples/demo/diagrams/dau-donut.dashdiagram` + card на Overview.

---

## 5. Как решать «добавлять kind или нет»

Чеклист перед новым kind в registry:

1. Какой **бизнес-вопрос** не закрывают bar/line/table/number/donut/heatmap?
2. Есть ли kind в **топ-практике** (census / Power BI defaults), а не только в AppSource?
3. Есть ли **DataFamily** (Chart / Table / Scalar / Matrix / …) или нужно новое?
4. Минимальный **payload + Host viz** без viz-plugin dll?
5. Demo на `samples/demo` + stdlib chrome в том же PR.

Если пункт 1–2 слабые — лучше presentation/transform, не kind.

---

## 6. Источники

- Tableau Research — [Dashboard design census](https://www.tableau.com/blog/tableau-research-understanding-dashboard-design-at-scale) (25 620 dashboards; bar 60%, line/map 25%, bespoke &lt;0.5%).
- Power BI practical set — [The Data Alchemist](https://thedataalchemist.co/data-insights/power-bi-charts/).
- Chart choosers — [Basedash](https://www.basedash.com/blog/how-to-choose-the-right-chart-for-a-dashboard), [ClicData](https://www.clicdata.com/blog/a-chart-chooser-for-bi-teams-stop-guessing-start-deciding/), [InetSoft tree](https://www.inetsoft.com/company/dashboard_visualization_tree/).

---

## 7. Следующий конкретный leaf (когда снимем передышку)

1. `number` KPI demo + stdlib presentation.  
2. Demo stacked bar (composition).  
3. ADR-amend / spike: `area` vs line fill.  
4. Только потом scatter.
