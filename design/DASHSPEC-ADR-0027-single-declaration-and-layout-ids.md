# DASHSPEC-ADR-0027: Single declaration and layout by canonical id

| | |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-07-05 |
| **Relates to** | [ADR-0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md), [ADR-0021](DASHSPEC-ADR-0021-dashlayout-include.md), [ADR-0025](DASHSPEC-ADR-0025-card-interior-layout-board.md), [ADR-0026](DASHSPEC-ADR-0026-layout-module-scope.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) |

## Context

Авторинг DashSpec упирается в **второй слой имён**:

- `card … ref Q` + `[ Q ]` в `.dashlayout` — два namespace (id vs token);
- `declarations { var … }`, `entities { }`, `SLOT = id` — registry поверх definition (отвергнуто как дубль);
- `logic { filter }` + `ui { filter }` — риск двойного объявления одной сущности.

При этом resolver **уже** принимает canonical id в board ([ADR-0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md): «ячейки — `ref` или `card` id»). Отдельный slot/ref слой не обязателен технически — только исторический shorthand.

Нужен north star: **как `var` в C#** — identifier объявлен **ровно один раз**; layout **ссылается**, не объявляет.

## Decision

### 1. Single declaration rule

В **resolved module** (tab module + expand includes) каждый identifier объявляется **один раз**:

| Kind | Declaration site | Identifier |
|------|------------------|------------|
| filter | `filter …` в report | filter **name** (`usage_date`) |
| card | `card <id> …` | card **id** |
| diagram (file) | `@diagram <id>` | diagram **id** |
| diagram (inline) | `diagram <kind> { … }` на card | anonymous; interior token `diagram` ([ADR-0025](DASHSPEC-ADR-0025-card-interior-layout-board.md)) |

Повторное объявление того же id → **error** (как duplicate `@diagram id` сегодня).

**Use** (не declaration): `bind`, `filters { … }`, `[ … ]` в layout, `diagram <id>` ref on card.

### 2. Layout tokens = canonical ids (no slot layer)

Bracket board (tab, toolbar, card interior, `.dashlayout`) содержит **только** id уже объявленных сущностей:

```text
# tab board — card ids
[ stakeholder_peak_by_app  stakeholder_utilization ]
[ stakeholder_peak_apps ]

# toolbar — filter names
[ usage_date  user_name  app_name ]

# card interior
layout {
  [ events_top ]
  [ lus_events_detail_table ]
}
```

- **Нет** `SLOT = …`, **нет** `entities { }`, **нет** второго registry.
- Короткая сетка — **выбор id при объявлении** (`card peak_apps …`), не alias layer:

```text
card peak_apps as "№2 …" { … }    # id короткий — layout [ peak_apps ]
```

Длинный product id (`stakeholder_peak_apps`) — тоже ok, если layout читаем.

### 3. `ref` — deprecated alias (transitional)

`ref Q` на filter/card ([ADR-0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md), [ADR-0022](DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md)) — **optional legacy**:

- v1: parser + resolver поддерживают `ref` и id (как сейчас);
- **канон для новых specs:** layout по **name/id**, без `ref`;
- duplicate `ref` среди filters/cards — error (уже есть);
- follow-up v2: warning → error on `ref`; удаление `LayoutRef` из model.

`ref` не расширяем (не `SLOT =`, не `declarations`).

### 4. Layout file ↔ spec

[ADR-0026](DASHSPEC-ADR-0026-layout-module-scope.md): `.dashlayout` + mandatory `scope`.

Validator после parse:

1. каждый token в `[ … ]` resolves к **ровно одному** declared id в scope (`toolbar` → filter name; `tab` → card id);
2. необъявленный token → error;
3. declared card/filter, требуемый board, но отсутствующий в layout — **warning** (lint), не error v1.

### 5. Export / embed — без `exports { }`

Export parent = семантика [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) `filters { }` / `standalone { }`, не отдельный registry.

Optional follow-up: modifier `export` на строке filter — одно declaration site:

```text
filter date period_start … export
```

Non-goal: `exports { usage_date, … }` как второй список id.

### 6. logic / ui split — optional packaging, not required

Разделение «данные vs подписи» — **профиль authoring**, не второй IR:

- **Monolith (default):** один `report { filter … card … }` — declaration и label на одной строке.
- **Split (optional):** machine attrs в `logic { }`, overlay `as` / `widget` / chrome в `ui { }` — **только ссылки на id**, без второго `filter` / `card` keyword (иначе duplicate declaration error).

## Examples

### Stakeholder tab (canonical ids in layout)

**`layouts/stakeholder-grid.dashlayout`:**

```text
@layout stakeholder_grid
scope tab

[ stakeholder_peak_by_app  stakeholder_utilization ]
[ stakeholder_peak_apps ]
[ stakeholder_peak_apps_period ]
[ stakeholder_idle ]
```

**`lus-dev-stakeholder.dashspec` (fragment):**

```text
report {
  filter date usage_date on usage_date as "Дата отчёта" default -7d..today
  …
  card stakeholder_peak_by_app as "№1 …" {
    diagram lus_stakeholder_peak_by_app_bar
    datasource view lus.v_peak_concurrent_by_period
    bind period_grain, period_start, app_name
  }
  card stakeholder_peak_apps as "№2 …" { … }
}
```

### Short-id profile (same rule, shorter board)

```text
card peak_by_app as "№1 …" { … }
card peak_apps as "№2 …" { … }
```

```text
[ peak_by_app  util ]
[ peak_apps ]
```

Один id — одно объявление; layout не вводит новых имён.

## Rejected

| Idea | Why |
|------|-----|
| `declarations { var … }` | registry + body = два места |
| `entities { }` / `exports { }` | то же |
| `SLOT = card_id` | магический второй namespace |
| `ref` + layout token без tie to id | «помни, что Q где» |
| `logic { filter }` + `ui { filter }` full duplicate | нарушает single declaration |

## Consequences

- **Authoring:** layout diff читается без lookup-таблицы ref→id.
- **LUS migration:** убрать `ref Q/E/…`, заменить tokens в `.dashlayout` на card/filter id (или укоротить card id).
- **Core (follow-up):** duplicate filter name / card id на module; lint undeclared layout token; deprecate `ref`.
- **Amends [ADR-0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md):** канон — id in board; `ref` legacy.

## Non-goals

- auto-shortening id (abbrev) в parser
- PlantUML-style relative layout
- actions / drill-down (отдельный ADR)
