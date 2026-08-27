# DASHSPEC-ADR-0029: Tooltip as entity (named slots + string)

| | |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-08-27 |
| **Supersedes** | earlier drafts (inspect-only chrome; raw `{sql_col}` templates; full EBNF / grammar tokens) |
| **Relates to** | [ADR-0004](DASHSPEC-ADR-0004-diagram-column-as.md), [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md), [ADR-0012](DASHSPEC-ADR-0012-host-presentation-layering.md), [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md), [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md) |

## Context

Сейчас tooltip размазан: колонка/`tooltip_time`/`format`/`split` в `heatmap { }`, склейка time+body в Core, Host только показывает. Measure и inspect-payload в одном блоке; нет нормального способа собрать текст из нескольких колонок без хардкода.

Дух (не буквальная копия CIDE `presentation` / EBNF): **ты сам обозначаешь позиции так, как хочешь** — даёшь слотам удобные имена, потом пишешь строку с `{этими_именами}`.

## Decision

### 1. Три слоя

| Слой | Что |
|------|-----|
| Measure | `heatmap` — только `x` / `y` / `value` |
| Tooltip | отдельная сущность `@tooltip` / `.dashtooltip` |
| Inspect / act | `inspect { use tooltip … }` · `on click { show … from tooltip }` |

Нет tooltip → value-only, `from tooltip` = resolve error.  
Tooltip есть, ячейка после подстановки пустая → нет inspect UX (не «Нет данных…»).  
Warm stub без drill-down → просто не объявлять tooltip.

### 2. Именованные позиции + строка

```text
@tooltip peak_hosts

variables
  peak_time = peak_bucket_hhmm
  hosts = peak_users_by_host
end variables

tooltip = "{peak_time}\n{hosts}"
label = "Одновременно в пике"
format = list
split = "; "
```

- `variables` — слот → SQL column; **имена слотов — твои**, любые удобные (`n1`, `hosts`, `peak_time`…).
- `tooltip = "…"` — обычная interpolated string: `{slot}` подставляется из row по RHS слота.
- `label` / `format` / `split` — chrome списка (как сейчас).

Одна колонка:

```text
@tooltip peak_apps

variables
  apps = peak_apps
end variables

tooltip = "{apps}"
label = "Состав в пике"
format = list
```

Или shorthand `source = peak_apps` (= один слот + `tooltip = "{value}"`).

Правила без церемоний:

- `{unknown}` → ошибка;
- SELECT = RHS слотов, реально встретившихся в строке;
- пустой результат → `null` в payload;
- `
` в строке — просто перевод строки (Presenter может взять первую линию как headline, если так написали).

Не делаем: EBNF в ADR, настраиваемые `{`/`}`, `@tooltipgrammar`, expressions в слотах.

### 3. Wiring

```text
@diagram lus_peak_concurrent_heatmap

include tooltip "../../tooltips/peak-hosts.dashtooltip"

heatmap
  x = usage_date
  y = app_name
  value = peak_concurrent_proxy
end heatmap

inspect
  use tooltip peak_hosts
end inspect
```

Регистрация: `!include "*.dashtooltip"` · `include tooltip "…"` · inline в diagram module.  
Card может override `inspect`. `from tooltip` → effective entity карточки.

### 4. Deprecated (один release)

`tooltip` / `tooltip_time` / `tooltip_format` / `tooltip_split` на heatmap → синтетический legacy tooltip + lint; потом parse error.

### 5. Pipeline (кратко)

Parse entity → resolve на card → SELECT RHS → per-cell render → Host показывает.  
Убрать `FormatCellTooltip` time-магию из Core.

## Non-goals

Mini-PL в строке, `on hover` behaviour, Chart.js tooltip для matrix, отдельный grammar-модуль.

## Consequences

Core: `TooltipDefinition` + module parser + include + validate.  
Host: presentation из entity.  
LUS: `.dashtooltip` + measure-only heatmaps.  
Amends ADR-0004 / ADR-0028.

## Implementation (after Accept)

1. Accept.
2. Model / parser / include / inspect wire / legacy.
3. Render + tests.
4. Host + LUS migrate + soak.
