# DASHSPEC-ADR-0003: Diagram kinds — registry вместо enum

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-06-29 |
| **Relates to** | [ADR-0002](DASHSPEC-ADR-0002-layout-and-presentation.md), demo heatmap cards |

## Контекст

`DiagramType` enum (`Line`, `Bar`, `Table`, `Number`) заставляет при каждом новом виде:

- править enum и парсер;
- разводить `switch` в Core, Host, QueryCompiler;
- дублировать схему свойств (`PropertySchemas.Diagram` одна на все типы).

Heatmap нужен для матричных отчётов (user × day, app × utilization). Это не «ещё один case», а **семейство matrix** с другими обязательными полями (`x`, `y`, `value`).

Коннекторы уже вынесены в plugins ([ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md)). Viz-plugins — следующий шаг; до них нужен **декларативный registry** в Core.

## Решение

### В spec — строковый kind (как сейчас в DSL)

```text
diagram heatmap {
  x = usage_date
  y = user_name
  value = peak_concurrent_apps
  height = 360
}
```

Синтаксис не меняется: `diagram <kind> { props }`.

### В Core — `DiagramKindRegistry`

Каждый kind описывается один раз:

| Поле | Смысл |
|------|--------|
| `Id` | `line`, `bar`, `pie`, `donut`/`doughnut`, `table`, `number`, `heatmap` |
| `DataFamily` | `Chart`, `Table`, `Scalar`, `Matrix` — для host/compiler без switch по каждому kind |
| `Properties` | допустимые свойства блока (как `PropertySchemas.FilterDate`) |
| `SupportsTopLimit` | `filter top` на карточке |

Парсер: `DiagramKindRegistry.Resolve(kind)` → схема → `PropertyBlockParser.Parse`.

Host и compiler смотрят на **`DataFamily`**, не на enum всех будущих chart-типов.

### Heatmap (v0.3)

- **Spec + registry + валидация** — сразу.
- **Рендер** — matrix payload (`xLabels`, `yLabels`, `cells[][]`) + JS (Chart.js matrix / canvas).
- **Query** — тот же `SELECT x, y, value …`; компилятор не меняется.

В demo soak: bar/table уже есть; heatmap — альтернативная карточка на тех же view.

## Отклонённые варианты

| Вариант | Почему нет |
|---------|------------|
| Расширять enum | Линейный рост switch; heatmap ≠ line |
| Свободный string без registry | Опечатки `heamap`, нет проверки `x`/`y`/`value` |
| Viz-plugins с v0.2 | Тяжелее loader + JS bundle; registry закрывает 90% |
| `diagram { type = heatmap … }` | Лишний уровень; kind в заголовке блока читаемее |

## Следующие шаги

1. ~~`BuildHeatmap` + `MatrixPayload` + renderer~~ — **✅ v0.3** (CSS grid в Host).
2. ~~Карточки heatmap в `demo-soak.dashspec`~~ — **✅**; дополнительные matrix-карточки — позже.
3. Опционально: viz-plugins (как connectors) для кастомных kind из dll.

## Последствия

- Новый **kind** = запись в registry + builder + (при необходимости) UI/JS.
- Новое **presentation-свойство** (`tooltip_format`, `color_scale`, …) на `chart`/`bar`/`heatmap` — **только spec + Host**, registry **не** трогаем (`AllowExtensionProperties`).
- Enum `DiagramType` удалён; breaking только для C# API host/tests, не для `.dashspec`.
