# DASHSPEC-ADR-0026: mandatory `scope` in `.dashlayout`

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-07-05 |
| **Relates to** | [ADR-0021](DASHSPEC-ADR-0021-dashlayout-include.md), [ADR-0022](DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md), [ADR-0025](DASHSPEC-ADR-0025-card-interior-layout-board.md) |

## Context

Bracket board в `.dashlayout` хранит **нетипизированные** токены (`[ Q E ]`). Смысл токена — `ref Q` на filter или card в parent spec. Один и тот же файл layout формально можно подключить и как toolbar, и как tab grid; автор должен «помнить», что где значит — особенно при `!include` из отдельного файла.

Include site уже задаёт resolver (`include toolbar` → filter refs; tab `!include layout` → card refs), но **не фиксирует намерение файла** и не ловит осознанный mismatch.

## Decision

### Обязательный `scope` в `.dashlayout`

После `@layout <id>` — строка `scope <kind>` **обязательна**. Без неё — parse error.

```text
@layout stakeholder_grid
scope tab

[ PEAK_APP  UTIL ]
[ HEAT_DAYS ]
```

```text
@layout soak_toolbar
scope toolbar

[ REPORT_DATE  USER  APPS ]
```

| `scope` | Токены в `[ … ]` | Include site |
|---------|------------------|--------------|
| `toolbar` | filter layout refs | `@dashboard` `!include`, `include toolbar` |
| `tab` | card layout refs | `@tab` module `!include`, `include layout` |
| `card` | card-interior refs (diagram slots) | reserved; inline `layout { }` в v1 |

### Static validation

При include parser проверяет **совпадение** `scope` файла и include site:

- `scope toolbar` + include в tab module → **error**
- `scope tab` + include на dashboard toolbar → **error**

Inline board (`toolbar { [ … ] }`, `layout { [ … ] }`, card interior) **без** `scope` — тип из синтаксиса блока (как раньше).

### Именование refs

`scope` не заменяет осмысленные `ref` (`PEAK_APP` vs `Q`). Рекомендация для новых specs — говорящие имена; validator scope не проверяет spelling.

## Non-goals

- `declarations { var … }` / второй словарь binding
- multi-scope в одном `.dashlayout` (`tab { }` / `filter { }` blocks)
- polymorphic reuse одного layout-файла на toolbar и tab
- `scope` в inline board

## Consequences

- **Breaking:** все `.dashlayout` без `scope` не парсятся
- Core: `LayoutScope`, `LayoutModuleScopeValidator`, `LayoutModuleParser`
- LUS: `scope toolbar` / `scope tab` в `docs/dashspec/layouts/*.dashlayout`
- Amends [ADR-0021](DASHSPEC-ADR-0021-dashlayout-include.md): формат файла включает `scope`
