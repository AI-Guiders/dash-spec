# DASHSPEC-ADR-0009: `bind` — единственный список фильтров карточки

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-06-29 |
| **Relates to** | [FILTERS_RU.md](../docs/FILTERS_RU.md), [ADR-0006](DASHSPEC-ADR-0006-sql-datasource-and-sqldialect.md) |

## Context

Ранее карточка дублировала одни и те же имена:

```text
bind usage_date, app_name
where [[usage_date]] and [[app_name]]
```

`where` выглядел как SQL, но компилятор только извлекал `[[name]]` и всегда склеивал через `AND`. Слово `and` не несло семантики. Колонки и операторы жили в `filter …`, не в `where`.

## Decision

На card — **только `bind`**:

```text
card peak as "Peak" {
  bind usage_date, app_name
  diagram lus_peak_concurrent_line
  datasource view lus.v_daily_peak_concurrent_proxy
}
```

`QueryCompiler` для каждого имени в `bind`:

| `FilterKind` | Действие |
|--------------|----------|
| `date` | `AND col >= @name_from AND col < upper(@name_to)` |
| `field` | `AND col = @…` или `IN (…)` |
| `top` | `TOP` / `LIMIT` на `SELECT`, **не** в `WHERE` |

Пустое значение фильтра → соответствующий `AND` не добавляется (как раньше для optional `[[…]]`).

`where` на card **удалён**; парсер отклоняет legacy-синтаксис с понятной ошибкой.

## Card-local и dashboard filters

Без изменений:

- `filters dashboard { … }` — фильтры в toolbar
- `filters { activity_day }` на card — виджет только на этой карточке
- `bind` — какие из них участвуют в запросе и chips

Пример table + top:

```text
card events_detail as "Events" {
  filters { events_top }
  bind app_name, user_name, events_top
  diagram lus_events_detail_table
  datasource view lus.v_events_detail
}
```

## Consequences

- Samples LUS: строки `where [[…]]` удалены.
- `CardDefinition.WhereTemplate` удалён из IR.
- Документация: [FILTERS_RU.md](../docs/FILTERS_RU.md).
