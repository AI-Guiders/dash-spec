# ADR-0057: Resolution registry — единая точка «кто побеждает»

**Status:** Accepted  
**Date:** 2026-09-25  
**Relates to:** [ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md), [ADR-0038](DASHSPEC-ADR-0038-structured-card-and-report-composition.md), [ADR-0039](DASHSPEC-ADR-0039-chart-chrome-merge.md), [ADR-0042](DASHSPEC-ADR-0042-host-control-center-witdb.md), [ADR-0054](DASHSPEC-ADR-0054-dashhost-module.md), [ADR-0056](DASHSPEC-ADR-0056-layout-card-groups.md)

## Context

Правила merge и SSOT размазаны по ADR-0023…0056. Автор spec **не должен** знать «кто побеждает» — это контракт **платформы**. Новые authoring-формы (block `title`, будущие `headers` / `.dashtitles`, GDL-таблицы) — **синтаксис**, не отдельная политика на каждый ADR.

В DSL **нет** `if` / ветвлений для resolution. Политика — **декларативная**: слот, цепочка источников (слабый → сильный), режим загрузки, fallback.

## Decision

### 1. Resolution registry — не файл автора

| Кто | Что |
|-----|-----|
| **Автор** | Пишет spec/catalog/layout в любом поддерживаемом виде |
| **Parser / expander** | Сводит источники в IR-поля |
| **Registry** | Один документ + один модуль кода: порядок источников на слот |
| **Host** | Читает **уже resolved** IR; live-override только для слотов из §Host |

Автор **не** создаёт `resolution.toml` и не задаёт precedence руками.

### 2. Нотация (meta, не grammar spec)

Слот:

```text
slot <dotted.name>
  mode <id> …
  chain weak → … → strong
  fallback <rule>
  surface <where in UI>
```

- **`chain`** — слева слабее, справа сильнее; **побеждает правый заданный** источник.
- **`mode`** — контекст загрузки (см. §3); не `if`, отдельные таблицы на режим.
- **`omit`** / **`suppress chrome`** — хост не рисует surface, значение в IR может оставаться.

Новый источник в authoring → **одна строка в chain** + parse hook; не новый абзац в пяти ADR.

### 3. Режимы (mode)

| Mode | Когда |
|------|--------|
| `catalog.prod` | Host открыл отчёт через `.dashcatalog` → `@tab` module |
| `dashboard.embed` | `@dashboard` + `tab { dashspec … }` |
| `soak.dev` | Прямой load `.dashspec` / dev upload без catalog |
| `host.ops` | Planet shell, theme, catalog path, WitDB |

Один слот может иметь **разные chain** по mode (таблицы ниже).

### 4. Registry v1 — display titles

Словарь свойств: [ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md) (`title`, `label`, `message`).

#### `report.header_title`

| Mode | Chain (weak → strong) | Fallback |
|------|------------------------|----------|
| `catalog.prod` | `catalog.entry.title` → `report.title` | `entry.id` |
| `dashboard.embed` | `report.title` → `catalog.entry.title` | `tab.id` |
| `soak.dev` | `report.title` | `document.title` → `tab.id` |

Surface: page `<h1>`, document title, catalog picker label (entry).

#### `tab.label`

| Mode | Chain | Fallback |
|------|-------|----------|
| `catalog.prod` | `catalog.entry.title` | `tab.id` |
| `dashboard.embed` | `tab.title` → `catalog.entry.title` | `tab.id` |
| `soak.dev` | `tab.title` | `tab.id` |

#### `page.nav_title`

| Mode | Chain | Fallback |
|------|-------|----------|
| all | `page.title` | `page.id` |

#### `card.chrome_title`

| Mode | Chain | Fallback | Suppress |
|------|-------|----------|----------|
| `catalog.prod` | `card.title` | `card.id` | chrome **omit**, когда `card.id` = `catalog.entry.id` (prod 1:1) |
| `dashboard.embed` | `card.title` | `entry.title` → `card.id` | — |
| `soak.dev` | `card.title` | `card.id` | — |

#### `layout_group.chrome_title`

| Mode | Chain | Fallback |
|------|-------|----------|
| all | `layout.group.title` | `layout.group.id` |

Surface: GroupBox header ([ADR-0056](DASHSPEC-ADR-0056-layout-card-groups.md)). Layout geometry — только id/ref; title не в bracket rows.

#### `catalog.group.title` / `catalog.entry.title`

| Slot | Chain | Fallback |
|------|-------|----------|
| `catalog.group.title` | `catalog.group.title` | `group.id` |
| `catalog.entry.title` | `catalog.entry.title` | `entry.id` |

Parse-time only; host picker.

#### Extension (не v1, зарезервировано)

| Source | Merges into slot |
|--------|------------------|
| `headers.card[id]` | `card.chrome_title` (перед `card.title` в chain) |
| `headers.group[id]` | `layout_group.chrome_title` |
| `@titles` module | то же, отдельный include |

Добавление — строка в chain, без смены mode tables.

### 5. Registry v1 — labels & messages

| Slot | Chain (weak → strong) | Fallback |
|------|------------------------|----------|
| `filter.label` | `filter.label` | `filter.name` |
| `diagram.axis_label` | `diagram.{binding}_label` → `diagram.label` on binding | binding name |
| `gate.placeholder_message` | `gate.message` | (empty → card hidden / placeholder per ADR-0030) |

### 6. Registry v1 — composition & chrome

#### `diagram.chart_chrome`

Chain ([ADR-0039](DASHSPEC-ADR-0039-chart-chrome-merge.md)):

```text
stdlib.preset
  → diagram.module.presentation
  → card.override_for[diagram_id]
  → card.inline_diagram.presentation
```

#### `diagram.series_transform`

```text
diagram.module.series_transform
  → card.override_for[diagram_id].series
```

Legacy `transform series` on card — deprecated → `override for` ([ADR-0038](DASHSPEC-ADR-0038-structured-card-and-report-composition.md)).

### 7. Registry v1 — layout geometry

| Slot | SSOT | Notes |
|------|------|-------|
| `tab.card_placement` | `.dashlayout` / inline `layout board` | bracket rows; [ADR-0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md) |
| `toolbar.filter_placement` | toolbar `.dashlayout` | [ADR-0022](DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md) |
| `card.interior_placement` | card interior board | [ADR-0025](DASHSPEC-ADR-0025-card-interior-layout-board.md) |

Explicit `place { }` on card — **override** поверх board ([ADR-0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md)).

### 8. Registry v1 — host & runtime

| Slot | Chain (weak → strong) | Notes |
|------|------------------------|-------|
| `host.catalog_path` | `dashhost.catalog` → `runtime.handoff` | [ADR-0054](DASHSPEC-ADR-0054-dashhost-module.md) |
| `host.theme` | `witdb.host_settings` → `dashhost.theme` | live ops [ADR-0042](DASHSPEC-ADR-0042-host-control-center-witdb.md) |
| `host.display_timezone` | `witdb` → `dashhost` → session default | |
| `connector.connection` | `runtime.toml` manifest | secrets never in DSL [ADR-0019](DASHSPEC-ADR-0019-runtime-directive.md) |
| `report.date_display` | `report.defaults` → `DateValueCodec` → `LabelFormat` | wire + cell + UI |

### 9. Implementation

| Layer | Responsibility |
|-------|----------------|
| `design/DASHSPEC-ADR-0057-resolution-registry.md` | Canonical tables (this ADR) |
| `DashSpec.Core.Resolution` (C#) | `Resolve(slot, mode, context)` — **target** |
| Parse / expand | Заполняют IR; не дублируют policy в Host |
| Host | `Resolve` или pre-merged IR; WitDB только для `host.*` slots |

**Phase P0 (this ADR):** registry document; прочие ADR ссылаются §4–§8 вместо повторения merge.  
**Phase P1:** `DashSpec.Core.Resolution.DisplayResolution` + `DisplayResolutionHost` + unit tests — **Done**.  
**Phase P2:** lint — два strong source на один слот без `override` → warning.

### 10. Authoring flexibility (одна политика)

| Author writes | Platform does |
|---------------|---------------|
| `card { title = "…" }` | `card.title` в chain |
| `headers { card id "…" }` (future) | inject before `card.title` |
| GDL row `card\|id\|title\|` (future) | same inject |
| nothing on card (prod 1:1) | suppress chrome per mode table |

Гибкость синтаксиса; **один** resolved slot.

## Consequences

- **ADR-0031** — vocabulary only; precedence → this ADR §4.
- **ADR-0039** — chrome merge → §6 `diagram.chart_chrome`.
- **ADR-0056** — group title → §4 `layout_group.chrome_title`.
- Новые ADR с merge: **запрещено** дублировать chain; только `See ADR-0057 §<slot>`.
- Authors: без изменений; registry невидим в `.dashspec`.

## Non-goals

- `if` / conditional grammar для resolution в DSL.
- Author-editable precedence files.
- i18n locale keys (follow-up).
