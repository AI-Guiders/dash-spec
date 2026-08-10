# DASHSPEC-ADR-0040: Chart recipes — coords / channels / marks (narrow)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-08-10 |
| **Relates to** | [ADR-0003](DASHSPEC-ADR-0003-diagram-kinds-registry.md), [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md), [ADR-0016](DASHSPEC-ADR-0016-bar-reference-markers.md) |

## Контекст

Standalone-линейка kinds растёт (`box`, `treemap`, `gauge`, …). Риск: каждый «красивый вид» = новый builder + ветка в payload + JS, хотя данные часто те же:

- category + measure → bar / donut / **wind rose**
- x + y (+ size) → scatter / bubble
- value samples → histogram / box

Полный grammar of graphics (Vega: свободная композиция marks) — другой продукт: комбинаторика валидации, query и DSL.

Нужна **узкая** модель: рецепты поверх registry, без «всё со всем».

## Решение

Сохранить ADR-0003 (`kind` + `DataFamily` + bindings). Добавить явную ментальную / implementation-рамку из трёх осей:

| Ось | Смысл | Примеры |
|-----|--------|----------|
| **Coords** | система координат рендера | `cartesian`, `polar`, `radial` (donut/gauge) |
| **Channels** | кодирование поверх того же payload | `size`→bubble, `fill`→area, `stack`, `reference` |
| **Marks / recipes** | именованный kind = фиксированный набор coords+channels+chrome | `windrose`, `area`, `gauge` |

Правила:

1. **Kind остаётся рецептом** в DSL (`diagram windrose { … }`), не свободным графом слоёв.
2. **Один DataFamily / один payload shape** на рецепт, когда данные совпадают с уже существующим builder’ом — **переиспользовать** (category aggregate → `CategoryChartPayloadBuilder`), менять только Host Chart.js `type` / chrome.
3. **Channels** — extension properties на chart kinds (`size`, `fill`, `reference`, …), не новые kinds.
4. **Запрещено в v0**: композиция kinds (`bar`+`sankey`), dual arbitrary overlays, пользовательский список marks в spec.

### Первый polar-рецепт: `windrose` / `wind_rose`

```text
windrose
  x = direction as "Dir"
  y = magnitude as "Wind"
end windrose
```

| Слой | Поведение |
|------|-----------|
| **Registry** | Chart family; bindings как category chart (`x`/`category`, `y`/`value`) |
| **Query** | тот же `SUM(measure) GROUP BY category` |
| **Payload** | `CategoryChartPayloadBuilder` (labels + values) |
| **Host** | Chart.js `polarArea` |

Demo может подставить любую category+measure (напр. peak by app) — форма данных wind rose, не обязательно метео-словарь.

## Отклонённые варианты

| Вариант | Почему нет |
|---------|------------|
| Свободная композиция kinds | комбинаторный ад; не упрощает Core |
| Сразу полный GoG / Vega-lite subset | слишком большой скачок для DashSpec DSL |
| Отдельный `WindRosePayload` | дублирует category payload |
| Только presentation `coord = polar` без kind | хуже discoverability в chooser / stdlib |

## Последствия

- Новые «похожие» kinds сначала проверять: тот же payload? → recipe + renderer.
- `ChartPayload` не раздувать без новой **формы данных**.
- Roadmap standalone: polar recipes (`windrose`) отдельно от flow (sankey) и geo (map).
- Следующие кандидаты в ту же рамку: явный `coord` в presentation chrome (опционально), combo bar+line **только** как заранее заданный recipe, не как DSL-граф.
