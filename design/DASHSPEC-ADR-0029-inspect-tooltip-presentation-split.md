# DASHSPEC-ADR-0029: Tooltip as entity (content only)

| | |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-08-27 |
| **Supersedes** | earlier drafts (format/split on tooltip; EBNF; raw SQL placeholders) |
| **Relates to** | [ADR-0004](DASHSPEC-ADR-0004-diagram-column-as.md), [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md), [ADR-0012](DASHSPEC-ADR-0012-host-presentation-layering.md), [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md), [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md) |

## Context

Tooltip сегодня смешивает **что** (колонки, склейка) и **как** (`tooltip_format` / `tooltip_split` / label в том же heatmap-блоке).  
Разделение: tooltip решает **содержание**; способ показа (список, inline, kv, …) — **другой слой**.

Дух именования: в `variables` слоты называешь как удобно; строка подставляет `{слоты}`.

## Decision

### 1. Три слоя

| Слой | Отвечает на | Примеры |
|------|-------------|---------|
| **Measure** | что на осях / в ячейке | `heatmap` → `x` `y` `value` |
| **Tooltip** | **что** показать при inspect/select | `@tooltip` → `variables` + `tooltip = "…"` |
| **Present / act** | **как** показать + действия | `inspect` · `on click { show below as list … }` |

Tooltip **не** задаёт list/inline/table. Это делают present-эффекты / inspect chrome.

### 2. Tooltip = только content

```text
@tooltip peak_hosts

variables
  peak_time = peak_bucket_hhmm
  hosts = peak_users_by_host
end variables

tooltip = "{peak_time}\n{hosts}"
```

| Поле | Смысл |
|------|--------|
| `variables` | слот → SQL column; имена — авторские |
| `tooltip` | interpolated string → текст (или `null`, если пусто) |

Shorthand: `source = peak_apps` ≡ один слот + `tooltip = "{value}"`.

Нет: `format`, `split`, `label` на `@tooltip`.

- `{unknown}` → ошибка;
- SELECT = RHS использованных слотов;
- пустой render → нет inspect payload.

### 3. Как показать — inspect / show

```text
inspect
  use tooltip peak_hosts
  label = "Одновременно в пике"
  as list
  split = "; "
end inspect
```

Или только через click (без hover chrome):

```text
on click
  show below as list from tooltip split "; "
end click
```

| Где | Что |
|-----|-----|
| `inspect` | hover/popover: `use tooltip`, опционально `label`, `as list\|inline`, `split` |
| `show below as …` | sticky detail: уже есть `list` / `plain` / `kv` ([ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md)); `split` — сюда же, если source = tooltip |

Default при `use tooltip` без `as`: Host может взять `list`, если в show/inspect задан `split`, иначе `inline` — уточнить при implement; канон: **явный `as` предпочтительнее**.

Merge: diagram `inspect` → card `inspect` override.

### 4. Wiring (diagram)

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
  label = "Одновременно в пике"
  as list
  split = "; "
end inspect
```

Регистрация tooltip: `!include "*.dashtooltip"` · `include tooltip` · inline в module.

### 5. Deprecated (один release)

Heatmap `tooltip` / `tooltip_time` / `tooltip_format` / `tooltip_split` / `as` на tooltip-колонке → legacy synthesizer (`variables` + string + `inspect` chrome) + lint; потом error.

### 6. Pipeline

Parse `@tooltip` (content) + `inspect`/`show` (presentation) → resolve → SELECT → render string per cell → Host применяет **present** rules к raw text.

Убрать Core-магию `FormatCellTooltip` (time+`\n`).

## Non-goals

- `format`/`split`/`label` на tooltip entity;
- EBNF / настраиваемые маркеры `{ }`;
- expressions в строке;
- tooltip решает table vs list.

## Consequences

- Content и presentation снова orthogonal (как measure vs chrome).
- ADR-0028 `show … as list|plain|kv` — канон «как»; tooltip — только source текста.
- LUS: `.dashtooltip` тонкий; list/split/label живут в `inspect` на diagram/card.

## Implementation (after Accept)

1. Accept.
2. Tooltip module (variables + string) + inspect block (use/label/as/split).
3. Move format/split/label off diagram props / MatrixPresentation-from-diagram.
4. Host + LUS migrate.
