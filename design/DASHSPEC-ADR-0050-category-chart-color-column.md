# DASHSPEC-ADR-0050: Category chart color — column binding

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-10 |
| **Relates to** | [ADR-0004](DASHSPEC-ADR-0004-diagram-column-as.md), [ADR-0014](DASHSPEC-ADR-0014-chart-series-colors.md), [ADR-0015](DASHSPEC-ADR-0015-dev-spec-resolve-dashboard-palette.md), [ADR-0016](DASHSPEC-ADR-0016-bar-reference-markers.md) |
| **Amends** | ADR-0014 (category charts; palette demoted to fallback) |

## Контекст

Category-диаграммы (bar, pie, donut, treemap, windrose) часто имеют **неизвестное на момент authoring число категорий**: whitelist растёт, TOP меняется, переименовываются display labels.

ADR-0014 решает цвет через `.dashpalette` / `color_palette` и hash или ordered list по **имени серии/метки**. Это:

- не масштабируется (автор не должен дописывать N цветов при росте данных);
- нестабильно при rename label;
- дублирует catalog, если цвет уже хранится в data-layer (admin, seed, migration).

Заказчик (remark №17) и эксплуатация требуют: **цвет задаёт тот, кто владеет данными** (catalog / API / view); spec только указывает **откуда читать колонку**.

## Решение

### Diagram binding: `color = <column>`

Для category chart kinds добавляется optional binding **`color`** — имя колонки result set (как `x`, `y`, `reference`).

```text
diagram lus_stakeholder_utilization_bar

bar
  x = app_name
  y = utilization_pct
  color = chart_color
end bar
```

- Синтаксис **симметричен** ADR-0004: `color = chart_color as "…"` допустим (`color_as` для подписи не используется).
- **`QueryCompiler` / `DiagramBindings.SelectedSqlColumns`** включают колонку `color`, если binding задан.
- Значение в row — **hex** `#rrggbb` (6 hex digits). Host/Core валидирует; невалидное / NULL → fallback (ниже).

### Runtime (Core → Host)

`CategoryChartPayloadBuilder` строит `ChartSeries.PointColors[]` per category:

| Приоритет | Источник | Когда |
|-----------|----------|--------|
| 1 | Semantic bar rules | `reference` marker ([ADR-0016](DASHSPEC-ADR-0016-bar-reference-markers.md)); превышение `y_max` / percent cap — фиксированный alert color |
| 2 | **`color` column** | binding задан, значение в row — valid hex |
| 3 | Host fallback | `ChartDefaultPalette` (generated pool); **не** требует `.dashpalette` в spec |

`transform series` / TOP-N **Other** сохраняет цвета по индексам исходных категорий; для агрегата Other — `default` fallback color.

Host `charts.js` без изменений контракта: использует `series[].pointColors[]` (hex + alpha suffix).

### Ответственность слоёв

| Слой | Ответственность |
|------|-----------------|
| **Data / catalog** (вне DashSpec Host) | Хранение цвета сущности (`chart_color`), UI admin, seed/migration |
| **SQL view** | Проброс колонки в result set (`chart_color`, join на catalog) |
| **Spec** | Wiring: `x`, `y`, `color = …` — **без** перечисления категорий и hex |
| **DashSpec Core/Host** | Чтение колонки, validation, semantic overrides, fallback pool |

Spec **не правится** при добавлении 50-го продукта в catalog — меняется только data-layer.

### `.dashpalette` и ADR-0014

**Для category charts с `color = column` — `.dashpalette` не обязателен.**

| Механизм ADR-0014 | Статус после ADR-0050 |
|-------------------|------------------------|
| `color = column` | **Канон** для category charts с catalog-backed цветом |
| `color_palette` / `.dashpalette` | **Optional legacy** — demo, air-gap, deployments без catalog column |
| `series_colors` inline | Optional override по имени (редко) |
| Hash / round-robin по label | **Deprecated** для category; удалить после migration slice |
| `ChartDefaultPalette` (Host) | Fallback при NULL / без binding |

Line / multi-series charts **без изменений**: ADR-0014 (`series`, `color_palette`) остаётся для time-series и named series.

## Non-goals

- Color picker в DashSpec Host Settings (цвет — data-layer, не WitDB presentation)
- Автоподбор контраста (a11y) — отдельный ADR
- HSL / CSS names в SQL column (только `#rrggbb` в v1)
- Heatmap (`color_scale`) — не затрагивается

## Последствия

### DashSpec (Core + Host)

- `DiagramKindRegistry`: property `color` (`ColumnBinding`) на category kinds
- `DiagramBindings.SelectColumnRoles`: добавить `color`
- `CategoryChartPayloadBuilder`: read row → `PointColors`; precedence table выше
- Tests: column present / NULL / invalid; reference marker overrides column

### Deployments (пример LUS)

- Migration: `lus.apps.chart_color VARCHAR(7) NULL`
- Admin API: редактирование цвета продукта
- Views: `… a.chart_color` в bar/pie datasources
- Spec: `color = chart_color`; **`use palette lus_apps` снимается** после rollout

### Документация

- ADR-0014: добавить ссылку «category charts → ADR-0050»
- Author guide: «цвет bar = колонка из view, не список в palette»

## Отклонённые варианты

| Вариант | Почему нет |
|---------|------------|
| `colors column = …` отдельный синтаксис | дублирует роль binding; `color =` достаточно |
| `colors from view.column` | view уже задан в `datasource`; лишний qualified path |
| Palette 256 в spec | не масштабируется; дублирует catalog |
| Только palette, без column | автор/data-layer разделение нарушено |

## Migration

1. Ship Core binding + Host fallback (обратно совместимо: без `color =` — старое поведение до удаления hash-path).
2. Data-layer column + admin UI.
3. Views + spec `color = chart_color` на stakeholder bars.
4. Deprecate `use palette lus_apps` в deployment specs; `.dashpalette` — archive/demo.
