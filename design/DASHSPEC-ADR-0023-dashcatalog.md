# DASHSPEC-ADR-0023: `.dashcatalog` and report catalog

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-07-02 |
| **Relates to** | [ADR-0011](DASHSPEC-ADR-0011-tab-modules.md), [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md), [ADR-0019](DASHSPEC-ADR-0019-runtime-directive.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) |

## Context

Host загружает `.dashcatalog` — whitelist отчётов для зрителя: автор добавляет `.dashspec` + entry в git.

Табы внутри soak — подразделы одного отчёта; catalog — **верхний** уровень (разные entry → разные `.dashspec`).

## Decision

### Файл `.dashcatalog`

```text
@catalog lus_dev

default = soak

entry soak as "License Usage — Dev Soak"
  dashspec "lus-dev-soak.dashspec"

entry stakeholder as "Отчёты заказчика"
  dashspec "lus-dev-stakeholder.dashspec"
```

| Элемент | Правило |
|---------|---------|
| `@catalog <id>` | корень файла; **без** outer `{ }` и inner `catalog { }` ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md)) |
| `default = <entry_id>` | опционально; иначе первая `entry` |
| `entry <id> as "Title"` | `dashspec "path"` — относительно каталога catalog |
| target | `@dashboard` или standalone `@tab` module |

Title entry (`title = "…"` in entry block) — display в catalog picker; для `@tab` target может дублировать / заменять optional title на `report` ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md)). Display vocabulary: [ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md) (`title` / `label`, без `as`).

### Host bootstrap

```toml
[dashboard]
catalog_path = "path/to/catalogs/lus-dev.dashcatalog"
```

Env: `DASHSPEC_CATALOG_PATH`. Host **требует** catalog; `spec_path` удалён.

### UI

Если catalog загружен и entries > 1 — dropdown «Отчёт» в header. Переключение перезагружает выбранный `.dashspec` (тот же `runtime.manifest`, если совпадает).

### Безопасность

Catalog — whitelist: только перечисленные пути. Upload в prod не даёт новых entry.

## Non-goals

- TOML `[[dashboard.catalog]]` (ops override — follow-up)
- Права на entry per-user
- Вложенные catalog

## Consequences

- LUS: `catalogs/lus-dev.dashcatalog` — flat `@catalog id` + entries
- Core: `CatalogParser` — flat body only ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md))
- Host: picker, session `LoadCatalogEntryAsync`, watcher `.dashcatalog`

## Amendment (ADR-0030, proposed)

Optional **`group <id> { title = "…"; entry { … } }`** в том же `.dashcatalog` — секции picker UI. Entry: `entry <id> { title = "…"; dashspec = "…" }`. См. [ADR-0030](DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md), [ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md). Ungrouped entries — backward compat.
