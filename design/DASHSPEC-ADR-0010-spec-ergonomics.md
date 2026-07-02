# DASHSPEC-ADR-0010: `on`, `toolbar`, `use card.*`, `bind dashboard`

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-06-29 |
| **Relates to** | [ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md), [ADR-0008](DASHSPEC-ADR-0008-viz-render-plugins.md) |

## Context

После ADR-0009 карточки всё ещё повторяли: фильтры (`column = … as`), `bind`, `diagram`, `datasource`, ручной `place`.

## Decision

### Фильтры: `on <column> as "Label"`

```text
filter date usage_date on usage_date as "Дата отчёта" default -7d..today
filter field app_name on demo.v_daily_active_users.app_name as "Products" widget combobox
filter top events_top as "Строк (TOP)" default 200
```

Legacy `filter date x { column = … as "…" … }` по-прежнему парсится.

### Toolbar вместо `filters dashboard`

```text
toolbar { usage_date, user_name, app_name }
toolbar chrome { layout = bar, apply = auto }
```

`filters dashboard` / `filters chrome` — алиасы.

### `bind dashboard` и auto-bind локальных фильтров

```text
card activity_5min as "Activity 5-min" {
  filters { activity_day }
  bind dashboard
  use demo_activity_5min
}
```

- `dashboard` → все фильтры из `toolbar { … }`
- `filters { … }` на card автоматически добавляются в bind (после parse)

### `[card.*]` в `@diagramlibrary`

```toml
[card.demo_peak_concurrent]
diagram = "demo_peak_concurrent_line"
datasource = "demo.v_daily_peak_concurrent_proxy"
bind = "dashboard"
```

```text
card peak_concurrent_proxy as "Peak concurrent (proxy)" {
  use demo_peak_concurrent
}
```

Override на card: `bind`, `diagram`, `datasource`, `legend`, …

### `place` опционален

Порядок карточек в `tab … cards { … }` + `TabLayoutCompactor` задают сетку; `place` только для исключений.

## Consequences

- Demo sample (`samples/demo/`) переписан под новый синтаксис.
- `CardResolver` объединяет card preset, bind expansion, diagram preset.
- Runtime-валидация top/table и datasource после resolve presets.

## Grammar (parse layer)

Формальная грамматика фильтров и дисамбигуация `filterBody` — в `FilterParser.cs` (комментарий ADR-0010).

Ключевое правило: **inline-свойства только на той же физической строке**, что и заголовок `filter …`; `{` на следующей строке — всегда `propertyBlock`. Пустое тело (`on … as "…"` + newline) — ε, без «перетекания» на следующий `filter`.

**Опциональные postfix-токены** (`ref`, trailing `as` после имени без `on`) — только на текущей строке: `TokenReader.TryKeywordSameLine` / `ParserUtilities.TryReadLayoutRef`. Обычный `TryKeyword` перед проверкой делает `SkipNewlines()` и ломает границу строк (следующий `filter` съедается как inline-свойство).

Слои после рефакторинга:

| Слой | Каталог |
|------|---------|
| Lexing | `Parsing/Lexing/` |
| Parse | `FilterParser`, `CardParser`, `DashboardParser`, … |
| Analysis | `Analysis/` (placement, tabs) |
| Resolution | `Resolution/SpecResolver` → `ResolvedDashboard` |
