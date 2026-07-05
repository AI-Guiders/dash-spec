# DASHSPEC-ADR-0018: `datasource sql` — query и file (без legacy)

| | |
|---|---|
| **Status** | Accepted · v0.6 |
| **Date** | 2026-07-02 |
| **Supersedes** | [ADR-0006](DASHSPEC-ADR-0006-sql-datasource-and-sqldialect.md) § «`datasource sql "…"`» |
| **Relates to** | [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md), [ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md) |

## Context

ADR-0006 задаёт compile-семантику и `@sqldialect`. Ранний черновик предлагал bare `datasource sql "…"` и heredoc `"""…"""` — **не реализовывались**. После ADR-0017 (файловые include) нужны явные носители **`query`** и **`file`**.

## Decision

### Иерархия (без изменений по смыслу)

| Синтаксис | Когда |
|-----------|--------|
| **`datasource view`** | Default — логика в БД (`schema.v_*`) |
| **`datasource sql query …`** | Ad-hoc SELECT / WITH до появления view |
| **`datasource sql file …`** | SQL в `.sql` рядом со spec (прототип, длинные отчёты) |

`sql from view` **не вводим** — для view остаётся короткий `datasource view`.

### Грамматика (inline + block, без legacy)

```text
datasource view lus.v_hourly_activity

datasource sql query "SELECT user_sam, MAX(n) AS peak FROM t GROUP BY user_sam"

datasource sql query [[
  SELECT user_sam, MAX(n) AS peak
  FROM t
  GROUP BY user_sam
]]

datasource sql file "sql/top-users.sql"

datasource sql {
  from query [[ … ]]
}

datasource sql {
  from file "sql/top-users.sql"
}
```

**Удалено (никогда не было в парсере или снято):**

- `datasource sql "…"` без `query` / `file`
- heredoc `"""…"""` (черновик ADR-0006)

### Многострочный inline: `query [[ … ]]`

| Форма | Когда |
|-------|--------|
| `query "…"` | одна строка |
| `query [[ … ]]` | несколько строк без экранирования `"` |
| `file "…"` | длинный SQL в `.sql` рядом со spec (предпочтительно для отчётов) |

Heredoc `"""…"""` **не вводим** — путаница с C#/Python, отдельный тип литерала в лексере.

`[[ … ]]` — raw-блок **только** после `datasource sql query` (или `from query` в block-form). Legacy `where [[filter]]` на card снят в [ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md); с TOML `[[plugins.load]]` в `.dashspec` не пересекается.

### Модель

```text
DataSourceKind.View  → Value = qualified name
DataSourceKind.Sql   → SqlCarrier = Query | File
                       Value = SQL body | relative path
```

- Путь **file** — относительно каталога корневого `.dashspec` (как `include diagram`).
- Тело **file** читается при **compile** (`QueryCompiler` + `specDirectory`); `SqlReadOnlyValidator` на содержимое.

### Dev watcher

Перезагрузка при изменении `.sql` в каталоге spec (как `.dashdiagram`).

## Non-goals v0.6

- `datasource sql from view` (дублирует `datasource view`)
- Подстановка `[[filters]]` внутрь SQL-файла
- `sql stdlib <…>` (как PlantUML)

## Consequences

- Core: `DataSourceParser`, `SqlDataSourceResolver`, `QueryCompiler` + `specDirectory`
- Host: `LoadedDashboard.SpecDirectory`, watcher `.sql`
- Тесты: query/file inline + block; отказ на bare `datasource sql "…"`
