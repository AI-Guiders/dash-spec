# DASHSPEC-ADR-0021: `.dashlayout` and `include layout`

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-07-02 |
| **Relates to** | [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md), [ADR-0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md) |

## Context

Tab layout board (`[ Q W ]` rows) в inline `tab { layout { … } }` работает, но:

- layout смешан с label/filters в tab-блоке;
- diff сетки не отделён от карточек;
- в `@tab` module преамбула уже несёт `@runtime`, `@palette` — board логичнее рядом.

## Decision

### Файл `.dashlayout`

```text
@layout stakeholder_grid

[ Q W ]
[ E ]
[ R ]
[ T ]
```

| Элемент | Смысл |
|---------|--------|
| `@layout <id>` | идентификатор модуля (как `@diagram`, `@palette`) |
| тело | строки `[ ref … ]` — та же грамматика, что inline board (ADR-0020) |

**Не в файле:** `layout grid { columns gap }` — остаётся на dashboard / shell `@tab`.

### `include layout` в shell `@tab` module

После `@tab <id>`, до `filter` / `card`:

```text
@tab stakeholder
include layout "layouts/stakeholder-grid.dashlayout"
```

Путь — относительно каталога корневого `.dashspec` (как `include diagram`). Расширение `.dashlayout` опционально.

### Inline alias

`tab { layout { [ … ] } }` по-прежнему допустим. **Нельзя** совмещать с `include layout` в одном модуле.

### Multi-tab `@dashboard`

В v1 layout board через include — только в **`@tab` module** (один tab = один файл). В корневом `@dashboard` с несколькими вкладками — `tab … dashspec "module.dashspec"` (layout внутри модуля).

Follow-up: `include layout "…" tab <id>` на dashboard shell.

## Dev watcher

Перезагрузка при изменении `.dashlayout` в каталоге spec.

## Non-goals

- stdlib `<layout/…>`
- доли/weights в `.dashlayout`
- `layout grid` в layout-файле

## Consequences

- Core: `LayoutModuleParser`, `LayoutParser.ParseBoardRows`
- LUS: `docs/dashspec/layouts/stakeholder-grid.dashlayout`
