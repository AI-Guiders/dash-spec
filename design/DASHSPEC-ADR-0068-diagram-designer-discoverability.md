# DASHSPEC-ADR-0068: Diagram Designer — discoverability через VIEW + diagram (не question entity)

| | |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-09-30 |
| **Relates to** | [ADR-0003](DASHSPEC-ADR-0003-diagram-kinds-registry.md), [ADR-0006](DASHSPEC-ADR-0006-sql-datasource-and-sqldialect.md), [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md), [ADR-0018](DASHSPEC-ADR-0018-sql-datasource-carriers.md), [ADR-0023](DASHSPEC-ADR-0023-dashcatalog.md), [ADR-0065](DASHSPEC-ADR-0065-card-interior-multi-slot.md) |

## Context

Классические BI (Metabase и аналоги) дают **discoverability** через GUI question builder: результат сохраняется как **сущность продукта** (question / saved query), часто с промежуточным языком (MBQL) и SQL, который автор не контролирует один в один.

DashSpec уже зафиксировал другую правду:

- **Данные** — `datasource view` (default) или ограниченный `datasource sql` ([ADR-0006](DASHSPEC-ADR-0006-sql-datasource-and-sqldialect.md), [ADR-0018](DASHSPEC-ADR-0018-sql-datasource-carriers.md)).
- **Визуал** — `diagram` kind + presentation ([ADR-0003](DASHSPEC-ADR-0003-diagram-kinds-registry.md), [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md)).
- **Деплой** — spec / diagram library в git, не JSON внутри BI-сервера.

Пробел: нет **интерактивного authoring** для ad-hoc срезов. Автор либо пишет SQL/view руками, либо уходит в Metabase «посмотреть и забыть». Цель — **перегнать Metabase в discoverability**, не копируя его модель, а усилив свою: exploration и shipped report — одна цепочка артефактов.

## Problem

| Metabase-style | DashSpec без Designer |
|----------------|------------------------|
| Быстрый первый график | Медленный старт без SQL-навыка |
| Question ≠ воспроизводимый DDL в БД | View в migrate — единственный SSOT |
| MBQL / generated SQL | SQL без искажений, dialect-aware compile |
| Плохой diff/review | Git-native spec + SQL |

Нужен UX «перетащил колонку — увидел таблицу/график», при этом **итог сохранения** — не opaque question, а **VIEW (или эквивалент в репо) + diagram module**, совместимые с существующим Host.

## Decision

### 1. Discoverability = Designer → Query Model → SQL → artifacts

Вводим **Diagram Designer** (Host UI + backend API), который редактирует не свободный текст SQL, а **каноническую модель запроса** (`ExploreQuery` / `ViewQueryModel` — имя в коде TBD):

| Слой модели | Примеры |
|-------------|---------|
| **From** | Базовые таблицы / опубликованные views (allowlist из semantic catalog) |
| **Join** | Declared joins (keys), без произвольного SQL join text в v1 |
| **Grain** | Группировка (dimensions), time grain на date-колонках |
| **Measures** | `count`, `count distinct`, `sum`, `avg`, `min`, `max` |
| **Filters** | Предикаты на колонках (в Designer — локальные; на card — `bind` как сегодня) |
| **Sort / limit** | Совместимо с `order_by` / table TOP ([ADR-0006](DASHSPEC-ADR-0006-sql-datasource-and-sqldialect.md)) |

**Компилятор модели** (тот же dialect, что `@sqldialect` / manifest):

1. **Preview** — `SELECT …` (с `LIMIT` / `TOP` для UI), без DDL.
2. **Publish** — `CREATE VIEW [schema].[name] AS <select>` (или `CREATE OR ALTER` под dialect + политику продукта).

Designer **не** становится вторым query runtime: после publish карточка использует обычный `datasource view` и существующий `QueryCompiler` для dashboard filters.

### 2. Связка с diagram kind

Designer работает в режиме **целевого diagram kind** (`table`, `bar`, `line`, `heatmap`, …):

- UI подсвечивает **обязательные привязки** (например heatmap: `x`, `y`, `value`).
- Preview рендерит через **тот же viz plugin**, что prod (TableHtml, ChartJs, …), на временном `CardRenderResult` / preview endpoint.
- При сохранении эмитится **`.dashdiagram`** (или фрагмент для diagram library) с `columns` / bindings, согласованными с projection view.

Так discoverability включает **«как это будет выглядеть в продукте»**, а не только grid строк.

### 3. Артефакты сохранения (SSOT)

| Стадия | Артефакты | Где живут |
|--------|-----------|-----------|
| **Draft** | `ExploreQuery` JSON + preview view name в sandbox schema | Host store / product DB registry |
| **Publish to repo** | `sql/views/<name>.sql` (тело view), `diagrams/<name>.dashdiagram`, опционально stub `card` в `.dashspec` | Git (продукт, напр. demo) |
| **Publish to DB only** | `CREATE VIEW` в `rpt` / `lus` schema | Migrate или controlled DDL job |

Правило: **опубликованный explore не остаётся только в Designer DB** — либо commit в репо (preferred), либо явный «DB-only» flag с audit (non-default).

`datasource sql` на card для долгоживущих отчётов **не заменяет** view: Designer — путь **создать view**, а не закрепить вечный inline SQL ([ADR-0006](DASHSPEC-ADR-0006-sql-datasource-and-sqldialect.md) escape hatch остаётся для edge cases).

### 4. Preview vs Publish (DDL)

| Действие | Поведение |
|----------|-----------|
| Drag column / change filter | Debounced **preview query** только `SELECT` |
| Save draft | Persist model + optional temp view в **explore schema** |
| Publish | DDL + запись артефактов + регистрация в semantic catalog |

На каждый жест **не** выполнять `CREATE VIEW` — latency, блокировки, мусор в каталоге БД.

### 5. Semantic catalog (read path для Designer)

Designer читает **allowlist** сущностей (tables/views, columns, types, friendly labels) — продуктовый catalog, не `INFORMATION_SCHEMA` без фильтра.

Минимум v1:

- Список базовых views (`demo.v_*`, `agg_*`) и ключевых fact tables.
- Колонки + роли (`dimension` / `measure` / `time`) — опционально в TOML/YAML рядом с demo schema docs.

Связь с roadmap «semantic catalog без имени view в каждой карточке» (demo ADR product boundaries): Designer **пишет** новые entries при publish (`explore.peak_by_app` → `demo.v_explore_peak_by_app`).

### 6. Безопасность и права

| Риск | Митигация |
|------|-----------|
| Произвольный DDL | Publish только через **DesignPublish** role; explore schema отдельно |
| SQL injection в Designer | Модель → parameterized SQL; имена идентификаторов из allowlist |
| Утечка данных | Row-level через базовые views; Designer не join'ит сырые таблицы вне catalog |
| DDL в prod connection | Рекомендация: **migration pipeline** (emit SQL file → review → `dotnet ef` / flyway / DBA), опция «apply via connector» только dev |

`SqlReadOnlyValidator` на **spec sql** не отменяется; тело view в репо проходит те же политики, что и ручные migrate.

### 7. Host surface (v1 vertical slice)

| Компонент | Ответственность |
|-----------|-----------------|
| `ExploreDesigner` (Razor/JS) | Canvas: fields, filters, kind picker, preview pane |
| `ExploreQueryCompiler` (Core) | Model → `SELECT` / `CREATE VIEW` per `SqlDialect` |
| `ExplorePreviewService` (Host) | Preview rows + skeleton `CardRenderResult` |
| `ExplorePublishService` (Host) | Write files (dev) / PR bundle / DDL job |
| Connectors | **Execute** preview; DDL — отдельный порт `ISchemaMutator` или вне Host |

Designer — **режим Host** (route `/designer` или entry из catalog), не отдельный продукт.

## Alternatives considered

| Вариант | Почему нет |
|---------|------------|
| Клон MBQL в DSL | Дублирует SQL, ломает «SQL без изменений», плохой diff |
| Только Monaco + `datasource sql` | Нет структуры, нет пути в view, слабая discoverability для нетехнарей |
| Встроить Metabase | Другая модель хранения, два SSOT, слабая кастомизация viz plugins |
| Designer без view (вечный explore id) | Не исполняется вне Host, DBA не видит, не переиспользуется |

## Phased delivery

| Phase | Scope | Outcome |
|-------|--------|---------|
| **P0** | `ExploreQuery` model + compiler → `SELECT` + `table` preview | API + тесты, без UI |
| **P1** | Minimal UI: pick base view, columns, filter, preview table | Ad-hoc table discoverability |
| **P2** | Publish → `.sql` view + `.dashdiagram` + `datasource view` stub | Git-native explore |
| **P3** | Kinds `bar` / `line`, measure grain | Chart discoverability |
| **P4** | Semantic catalog CRUD on publish, draft lifecycle, GC explore schema | Ops-ready |

## Non-goals (initial phases)

- Произвольный SQL join text в Designer (только declared joins)
- Pivot / crosstab как отдельный kind (может быть P5+ через другой view shape)
- Замена ETL / `agg_*` backfill
- Social features (share question, pulses) — вне DashSpec
- Автоматический `CREATE VIEW` на prod без review по умолчанию

## Consequences

- **Core**: новый пакет или папка `Explore` / `ViewGeneration` рядом с `QueryCompiler`; расширение `SqlDialect` для view DDL header/footer.
- **Host**: designer routes, preview endpoints, опционально WebSocket для debounced preview.
- **Products (demo)**: schema `lus` + `explore` (или `demo_explore`); catalog entries; CI проверяет, что опубликованные view names уникальны.
- **Connectors**: чёткое разделение read (`QueryAsync`) vs schema mutate (может быть вне v1 connector).
- **Docs**: Author Guide — «Prototype in Designer → publish view → reference in card».

## Success criteria

1. Автор без ручного `CREATE VIEW` получает **ту же** карточку, что если бы написал view + diagram в репо.
2. Preview SQL **байт-в-байт** (modulo limits) совпадает с телом опубликованного view.
3. Опубликованный explore diff'ится в git (SQL + diagram), проходит review.
4. Metabase остаётся опциональным для «грязного» raw poke; **product discoverability** живёт в DashSpec.

## Open questions

1. Имя explore view: `v_explore_{uuid}` vs `{slug}` vs catalog id.
2. Обязательность commit при publish или допустимый «DB-only» режим для личных черновиков.
3. Версионирование view при изменении модели (new view vs `ALTER`).
4. Единый `ExploreQuery` serialization в sidecar `.explore.json` рядом с diagram или только SQL как SSOT.
