# DASHSPEC-ADR-0006: `datasource sql` и `@sqldialect`

| | |
|---|---|
| **Status** | Accepted · v0.3 (partial) |
| **Date** | 2026-06-29 |
| **Relates to** | [ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md), [FILTERS_RU.md](../docs/FILTERS_RU.md) |

## Context

- **`datasource view`** — основной путь: логика в SQL views БД (`schema.v_*`; в sample — `demo.v_*`), DashSpec только фильтры и визуал.
- На краях нужен **native SQL** (top-N, CTE, прототип до migrate) без дублирования всей модели в views.
- Парсер уже принимал `datasource sql "…"`, рантайм — `NotSupportedException`.
- Разные коннекторы → разный синтаксис дат/лимитов (`DATEADD` vs `INTERVAL`).

## Decision

### Иерархия источников

| Уровень | Когда |
|---------|--------|
| **`datasource view`** | Default. Доменные отчёты, одна правда в БД. |
| **`datasource sql`** | Escape hatch: ad-hoc, прототип, запрос не укладывается в `SELECT … FROM view WHERE …`. |
| **View в БД** | Если SQL в spec живёт дольше пары итераций → перенос в migrate продукта. |

### File directives (преамбула)

```text
@config "demo.toml"
@sqldialect tsql

@dashboard demo_soak
dashboard "…" { … }
```

| Директива | Обязательность | Значения |
|-----------|----------------|----------|
| `@config` | **да** | путь к TOML (как ADR-0001) |
| `@sqldialect` | нет | `tsql` (default), `postgres`, `generic` |

- **`@sqldialect`** задаёт диалект для **компиляции фильтров** из `bind` (date range, field `IN`, `TOP` vs `LIMIT`).
- Default **`tsql`** — SqlServer connector на dev.
- В будущем: `default_dialect` в manifest коннектора, если `@sqldialect` опущен.

### Семантика `datasource sql`

```text
card top_users as "Top users" {
  bind usage_date, user_name
  diagram bar { x = user_sam y = peak_concurrent_apps }
  datasource sql """
    SELECT user_sam, MAX(peak_concurrent_apps) AS peak_concurrent_apps
    FROM demo.v_daily_peak_concurrent_apps_per_user
    GROUP BY user_sam
  """
}
```

Компилятор:

1. Оборачивает тело SQL: `( <user sql> ) AS _dashspec_q`
2. Снаружи: `SELECT <cols> FROM (…) AS _dashspec_q WHERE 1=1 AND …` — фильтры из `bind` (date/field), как у view.
3. `order_by` / `TOP` (table) — как для view.
4. Запрещено: `;`, несколько statements (валидация v0.3+).

**Фильтры внутри SQL-строки** — **не в v0.3**; только `bind` на card ([ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md)).

### Диалект и фильтры

| Конструкция | `tsql` | `postgres` |
|-------------|--------|------------|
| Конец дня (date filter) | `col < DATEADD(day, 1, @to)` | `col < (@to::date + INTERVAL '1 day')` |
| Table limit | `SELECT TOP n` | `LIMIT n` (v0.3+: trailing) |
| `generic` | как `tsql` | для неизвестных движков |

Коннектор по-прежнему только **выполняет** `CompiledQuery`; диалект — зона **Core / QueryCompiler**.

## Non-goals v0.3

- `sql file "queries/foo.sql"` (отдельный файл) — отдельный ADR
- Подстановка `[[filters]]` внутрь heredoc
- Hot reload `@sqldialect`

## Безопасность SQL (текст spec)

**Проверка на этапе parse** (`SqlReadOnlyValidator` в Core), не на коннекторе и не «надеемся на read-only УЗ»:

| `datasource` | Правило |
|--------------|---------|
| **view** | только qualified name `schema.object` |
| **sql** | один statement, без `;`, без комментариев, начало `SELECT` / `WITH`, запрет DML/DDL/exec и `SELECT INTO`, `OPENROWSET`, `xp_*`, … |

Строковые литералы вырезаются перед сканом ключевых слов (`'DELETE'` в данных — ок).

Повторная проверка в `QueryCompiler.WrapSqlDataSource` — defense in depth при compile.

## Consequences

- Demo sample: `@sqldialect tsql` явно в `demo-soak.dashspec`.
- Ad-hoc SQL в отдельном cookbook продукта остаётся вне DashSpec; `datasource sql` — только где view избыточен.
- Новый connector Postgres → `@sqldialect postgres` + connector plugin.

## Пример (ad-hoc sql, когда view избыточен)

Top-N за период без отдельного view — допустимый кандидат на `datasource sql`; heatmap на view остаётся как есть.
