# DASHSPEC-ADR-0025: Card interior layout board

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-07-05 |
| **Relates to** | [ADR-0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md), [ADR-0022](DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md) |

## Context

Tab-level board (ADR-0020) раскладывает **карточки** на вкладке. Toolbar board (ADR-0022) — **dashboard-фильтры**. Внутри card host рисовал фиксированный стек: local filters → chips → diagram.

Нужна та же bracket-модель для **слотов карточки**: diagram, local filters (TOP и др.).

## Decision

### Слоты

| Слот | Как задаётся | Токен board |
|------|----------------|-------------|
| diagram | обязателен | `diagram ref D` → `D`; без ref → зарезервированный токен `diagram` |
| local filter | `filters { name }` на card | `ref` фильтра или имя |

**Не в board (v1):** bound chips, legend, title — остаются в `card-head` над grid.

### Синтаксис

```text
card events_detail as "Детализация" ref E {
  filters { events_top }
  diagram ref D lus_events_detail_table
  datasource view lus.v_events_detail
  bind usage_date, user_name, app_name

  layout {
    [ T ]
    [ D ]
  }
}
```

Фильтр объявлен выше с `ref T`. `layout { … }` внутри card — **только** bracket rows (не `layout grid`).

### Grid

Тот же `LayoutBoardPlacer` и `columns` dashboard (`layout grid` / default 12).

### Без `layout { }`

Host сохраняет legacy: horizontal local filters + diagram снизу.

### Валидация

При наличии board:

- diagram-слот ровно один раз;
- каждый local filter card ровно один раз;
- лишние токены → ошибка.

### Namespace ref

`ref` на **card** (E) — tab-level (ADR-0020). `ref` на **diagram** (D) и **filter** (T) — card-interior; не пересекаются.

## Non-goals (v1)

- `include layout` внутри card
- board для bound chips / legend
- отдельный `columns` на card

## Consequences

- Core: `CardDefinition.InteriorBoard`, `DiagramSlotRef`, `CardInteriorLayoutCompactor`, `CardInteriorSlotResolver`
- Host: `.card-interior-grid`, placement на слотах
- LUS: `lus-dev-detail.dashspec` — пример `[ T ] / [ D ]`
