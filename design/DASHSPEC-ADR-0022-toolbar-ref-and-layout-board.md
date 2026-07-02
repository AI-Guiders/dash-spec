# DASHSPEC-ADR-0022: Filter `ref` and toolbar layout board

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-07-02 |
| **Relates to** | [ADR-0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md), [ADR-0021](DASHSPEC-ADR-0021-dashlayout-include.md), [ADR-0010](DASHSPEC-ADR-0010-spec-ergonomics.md) |

## Context

Toolbar — плоский `toolbar { usage_date, user_name, … }`. На soak 5+ фильтров в одной строке нечитаемы. ADR-0020 дал `ref` и bracket-board для карточек; та же модель подходит для toolbar (тоже grid).

Отдельный `.dashtoolbarlayout` не вводим — переиспользуем `.dashlayout` (ADR-0021).

## Decision

### `ref` на фильтре

После `as "Label"` / `on … as "Label"`, до `{` или inline-свойств:

```text
filter date usage_date on usage_date as "Дата" ref D default -7d..today
filter field app_name on lus.v_daily_active_users.app_name as "Продукты" ref A widget combobox
```

`ref` опционален; каноническое имя — `filter` ident (`usage_date`).

### Toolbar board (inline)

Та же грамматика строк `[ … ]`, что tab layout (ADR-0020):

```text
toolbar {
  [ D P G ]
  [ U A ]
}
```

Допустимо без обёртки, если сразу `[`:

```text
toolbar [ D P G ] [ U A ]
```

Legacy `toolbar { usage_date, user_name }` — одна неявная строка (row-major порядок).

`filters dashboard { … }` — alias; board-синтаксис тоже поддерживается.

### Grid

Строка board мапится на `layout grid` dashboard (`columns`, по умолчанию 12) — **тот же алгоритм**, что `TabLayoutBoardResolver`:

| Ячеек в строке | span | col |
|----------------|------|-----|
| 1 | full | 1 |
| N | `columns / N` | `1 + i * span` |

Host рендерит toolbar через CSS grid (`--grid-columns`) и `grid-column` / `grid-row` на ячейках.

`toolbar chrome { layout = bar }` — только стиль ячеек (inline vs card), не отдельная геометрия.

### `include toolbar` + `.dashlayout`

В dashboard shell (или standalone `@tab` с toolbar):

```text
include toolbar "layouts/soak-toolbar.dashlayout"
```

Файл — тот же формат, что ADR-0021:

```text
@layout soak_toolbar

[ D P G ]
[ U A ]
```

Нельзя совместить `include toolbar` и inline toolbar board.

### Модель

- `FilterDefinition.LayoutRef?`
- `DashboardDocument.ToolbarBoard?` — сырые токены board
- `DashboardDocument.DashboardFilters` — resolved имена фильтров (row-major), как сейчас для bind/state

### Валидация

- `ref` уникален среди фильтров
- каждый токен board → `ref` или имя фильтра
- каждый фильтр в board ровно один раз
- top-фильтры по-прежнему запрещены в toolbar

## Non-goals

- `.dashtoolbarlayout`
- `place` на фильтре (follow-up)
- отдельный `toolbar layout grid` (делит dashboard grid)

## Consequences

- Core: `FilterLayoutRefResolver`, `LayoutBoardPlacer`, `ToolbarPlacementParser`, `ToolbarLayoutCompactor`
- Host: grid на `.filters-grid`, placement style на фильтрах
- LUS: `layouts/soak-toolbar.dashlayout`, refs в `lus-dev-soak.dashspec`
