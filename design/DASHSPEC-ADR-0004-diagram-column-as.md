# DASHSPEC-ADR-0004: Column binding — `column as "Label"`

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-06-29 |
| **Relates to** | [ADR-0003](DASHSPEC-ADR-0003-diagram-kinds-registry.md), PlantUML `as` |

## Контекст

В `diagram` свойства `x`, `y`, `value`, `tooltip` — это **имена колонок SQL**. Подписи для UI (оси, tooltip) либо дублировались через `label` на фильтрах, либо хардкодились в Host (`FormatUserLabel`).

Нужен единый декларативный синтаксис, как **PlantUML `as`**: идентификатор для данных + человекочитаемая подпись.

## Решение

### Синтаксис

```text
diagram heatmap {
  x = usage_date as "День"
  y = user_name as "Пользователь"
  value = peak_concurrent_apps as "Разных ПО"
  tooltip = peak_apps as "Состав в пике"
}
```

- **`as` опционален** — `x = usage_date` по-прежнему валиден.
- **Подпись** — строка в кавычках после `as`.
- **Колонка** — identifier или qualified name (`lus.v_events_detail.user_name`).

### IR (внутри `DiagramDefinition.Properties`)

| Ключ | Значение |
|------|----------|
| `x` | `usage_date` |
| `x_as` | `День` |

Парсер добавляет `{key}_as` только при наличии `as`. Host/Core читают через `DiagramBindings.Column` / `DiagramBindings.Label`.

### Card

```text
card stakeholder_peak_apps as "№2 **Peak apps** per user" {
  diagram heatmap { … }
}

tab stakeholder as "Отчёты заказчика" {
  cards {
    stakeholder_peak_by_app
    stakeholder_peak_apps
    stakeholder_idle
  }
}
```

- **`Id`** — identifier (`snake_case`), в `tab.cards` только id.
- **`Title`** — display string после `as` (Creole-subset, см. ADR-0005).

### Filter (date / field)

```text
filter date usage_date {
  column = usage_date as "Дата отчёта"
  default = -7d..today
}
```

Подпись из `column as "…"` попадает в `FilterDefinition.Label`. У **`filter top`** — `filter top name as "Label" { … }`.

### Scope v0.3

| Область | `as` |
|---------|------|
| heatmap `x`, `y`, `value`, `tooltip` | ✅ |
| line/bar `x`, `y`, `series`, `value` | ✅ parse + `ChartPresentation` (оси — позже в JS) |
| `card id as "Title"` | ✅ id в tab, title в UI |
| filter `column` (date/field) | ✅ `column = col as "Label"` |
| filter `top` | ✅ `filter top name as "Label" { default = N }` |
| tab | ✅ `tab id as "Label" { cards { id … } }` |
| table `columns` | ☐ (comma-list, отдельный шаг) |

### SQL

`QueryCompiler` в `SELECT` берёт только column-часть (`x`, `y`, …). `{key}_as` не попадает в запрос.

### Tooltip (heatmap)

```text
tooltip = peak_apps as "Состав в пике"
tooltip_format = list
tooltip_split = ", "
```

| Свойство | Значения | Смысл |
|----------|----------|--------|
| `tooltip_format` | `list` · `inline` | `list` — popover со списком; `inline` — одна строка |
| `tooltip_split` | строка | разделитель элементов списка (по умолчанию `, `) |

Если задан `tooltip`, default для `tooltip_format` — **`list`**.

Host рендерит **popover** (не нативный `title`), чтобы список ПО читался построчно.

`tooltip_format` / `tooltip_split` — **extension properties** (см. [ADR-0003](DASHSPEC-ADR-0003-diagram-kinds-registry.md)): в `.dashspec` пишем без правки `DiagramKindRegistry`; Host/`MatrixPresentation` читают из `diagram.Properties`.

### Формат подписей осей (heatmap)

```text
x_format = date.short
y_format = user.short
color_scale = heat
```

| Свойство | Значения | Смысл |
|----------|----------|--------|
| `x_format` | `date.short` · `date.iso` · `raw` · `truncate.22` | формат меток по X |
| `y_format` | `user.short` · `raw` · `truncate.22` | формат меток по Y |
| `color_scale` | `heat` · `mono` | палитра ячеек и градиента легенды |

`user.short` — часть после `\`, обрезка до 22 символов. `date.short` — `dd.MM`.

Host **не** подставляет доменные fallback-подписи; всё задаётся в spec (`as`, `legend`, форматы).

### Легенда (отдельный блок карточки)

```text
card "№2 …" {
  diagram heatmap { … }
  legend {
    min = "мин. разных ПО"
    max = "макс. {max}"
  }
  datasource …
}
```

- Блок **`legend`** — на уровне **card**, не внутри `diagram` (как `place`, `bind`).
- `min` / `max` / `title` — строки; в тексте допустимы плейсхолдеры `{min}` и `{max}` (min/max значения матрицы).
- Host рисует легенду **под сеткой heatmap**, отдельным flex-блоком с градиентной полосой (`data-color-scale` из `color_scale`).

## Последствия

- Spec №2 читается без знания Blazor-хелперов.
- Host опирается на `_as`, `x_format`/`y_format`, `legend` — без `FormatUserLabel` и захардкоженных «разных ПО» / «макс.».
- Rich text в whitelisted строках — [ADR-0005](DASHSPEC-ADR-0005-rich-text-creole-subset.md) (Creole-subset, не PlantUML engine).
