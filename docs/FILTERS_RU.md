# Где живут фильтры в DashSpec

Кратко: **спецификация фильтров — в `.dashspec` (git)**. **Коннектор** только выполняет SQL. **Host** рисует UI. **Core** компилирует `bind` в `WHERE` / `TOP`.

## Слои

```
┌─────────────────────────────────────────────────────────────┐
│  samples/*.dashspec  (источник правды, в git)               │
│    filter date … default -7d..today                         │
│    filter field app_name column demo.v_….app_name            │
│    card … bind usage_date, app_name                         │
└───────────────────────────┬─────────────────────────────────┘
                            │ parse
                            ▼
┌─────────────────────────────────────────────────────────────┐
│  DashSpec.Core — IR + компиляция (не плагин)                │
│    FilterDefinition  — что за фильтр (date / field / top)   │
│    FilterState       — текущие значения на сессии           │
│    QueryCompiler     — bind → AND col >= @… / TOP n         │
└───────────────────────────┬─────────────────────────────────┘
                            │ CompiledQuery
                            ▼
┌─────────────────────────────────────────────────────────────┐
│  Connector plugin (sqlserver, …) — только QueryAsync         │
└─────────────────────────────────────────────────────────────┘
                            ▲
┌───────────────────────────┴─────────────────────────────────┐
│  DashSpec.Host — UI фильтров (Blazor)                       │
└─────────────────────────────────────────────────────────────┘
```

## Три места — три роли

| Место | Что хранит | Пример |
|-------|------------|--------|
| **`.dashspec`** | объявление фильтра | `filter date usage_date column usage_date default -7d..today` |
| **`.dashspec` (card)** | какие фильтры у карточки | `bind usage_date, app_name` |
| **`FilterState` (runtime)** | выбранные значения | `2026-06-01…2026-06-07`, `["Tekla Structures"]` |

## `bind` — один список на карточку

```text
card peak as "Peak concurrent" {
  bind usage_date, app_name
  diagram demo_peak_concurrent
  datasource view demo.v_daily_peak_concurrent_proxy
}
```

Компилятор сам строит SQL:

- `usage_date` (date) → `AND usage_date >= @usage_date_from AND usage_date < …`
- `app_name` (field) → `AND app_name = @app_name_0` или `IN (…)`
- `events_top` (top) → `SELECT TOP 200 …`, не `WHERE`

Пустой фильтр → соответствующее условие не добавляется.

Старый синтаксис `where [[usage_date]] and [[app_name]]` **удалён** ([ADR-0009](../design/DASHSPEC-ADR-0009-bind-only-filters.md)).

## Поля `FilterDefinition`

| Поле | Смысл |
|------|--------|
| `Name` | имя переменной (`usage_date`, `app_name`) |
| `Kind` | `date`, `field` или `top` |
| `ColumnReference` | колонка в SQL |
| `DefaultExpression` | диапазон дат **в spec** (см. ниже) |

## `default -7d..today` — синтаксис в spec

```text
filter date usage_date column usage_date default -7d..today
```

| Граница | Форма | Пример |
|---------|--------|--------|
| относительная | `-Nd` | `-7d` → today − 7 |
| якорь | `today` | текущий UTC-день |
| абсолютная | `yyyy-MM-dd` | фиксированная дата |

Парсер: `DateDefaultRange`. Неизвестная форма → ошибка parse.

## Размещение фильтров

| Конструкция | Роль |
|-------------|------|
| `filter …` | объявление: тип, колонка, default, label |
| `filters dashboard { … }` | фильтры в общем toolbar |
| `filters { activity_day }` на card | виджет только на этой карточке |
| `bind a, b` | **какие фильтры участвуют в запросе и chips** |

Пример soak:

| Карточка | `bind` |
|----------|--------|
| Peak, DAU | `usage_date`, `app_name` |
| Activity 5-min | `activity_day`, `app_name` |
| Events detail | `app_name`, `user_name`, `events_top` |

## См. также

- [design/DASHSPEC-ADR-0009-bind-only-filters.md](../design/DASHSPEC-ADR-0009-bind-only-filters.md)
- [design/DASHSPEC-ADR-0001-connectors-as-plugins.md](../design/DASHSPEC-ADR-0001-connectors-as-plugins.md)
- [samples/demo/demo-soak.dashspec](../samples/demo/demo-soak.dashspec)
