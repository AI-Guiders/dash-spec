# DASHSPEC-ADR-0029: Tooltip entity + template grammar

| | |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-08-27 |
| **Supersedes** | earlier draft of this ADR (inspect chrome only; `tooltip = col` on heatmap) |
| **Relates to** | [ADR-0004](DASHSPEC-ADR-0004-diagram-column-as.md), [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md), [ADR-0012](DASHSPEC-ADR-0012-host-presentation-layering.md), [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md), [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md), [ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md) |
| **Pattern borrow** | Cascade IDE [ADR-0017](../cascade-ide/docs/adr/0017-multi-window-workspace-and-agent-surfaces.md) (`presentation` string + `[presentation_grammar]`) · [ADR-0032](../cascade-ide/docs/adr/0032-hud-banner-configuration-and-grammar.md) (HUD banner template intent) |

## Context

Heatmap «tooltip» сегодня — не сущность, а размазанный контракт:

1. **Diagram** — `tooltip = peak_apps as "…"`, плюс `tooltip_time` / `tooltip_format` / `tooltip_split` в `heatmap { }` ([ADR-0004](DASHSPEC-ADR-0004-diagram-column-as.md)).
2. **Core** — `MatrixPayloadBuilder.FormatCellTooltip` склеивает time + body через `\n` (магия в коде).
3. **Host** — hover popover / `show below … from tooltip` читают уже склеенную строку.

Проблемы:

- measure (`x`/`y`/`value`) и inspect-payload живут в одном блоке диаграммы;
- drill-down формат (list/split/label) — presentation, но пишется рядом с SQL binding;
- warm stub с `NULL` tooltip-колонками даёт UX «Нет данных…», хотя ячейка value есть;
- нет общего способа собрать текст из **нескольких** колонок без хардкода.

Нужен канон как у CIDE `presentation`: **именованная сущность + строка-шаблон + опциональная грамматика токенов + EBNF**, узкий ручной парсер, без mini-PL.

## Decision

### 1. Три слоя (orthogonal)

| Слой | Сущность | Пример |
|------|----------|--------|
| **Measure** | `heatmap` / chart kind | `x`, `y`, `value` |
| **Tooltip** | `@tooltip` / `.dashtooltip` | `template`, `label`, `format`, `split` |
| **Inspect / act** | `inspect` + `on click` | hover popover; `show below list from tooltip` |

- **Нет блока tooltip** → value-only heatmap: hover-detail только по value (без list/inspect UX), `from tooltip` в card — **ошибка resolve**.
- **Есть tooltip, ячейка после render пустая** → нет hover inspect / пустой list (не «Нет данных для выбранной ячейки»).
- Warm stub без drill-down колонок → **не объявлять** tooltip в spec (не маскировать NULL в SQL «для совместимости»).

### 2. Tooltip — first-class module

Файл `.dashtooltip` (зеркало `.dashpresentation` / `.dashdiagram`):

```text
@tooltip peak_hosts

label = "Одновременно в пике"
format = list
split = "; "
template = "{peak_bucket_hhmm}\n{peak_users_by_host}"
```

| Поле | Слой | Смысл |
|------|------|--------|
| `template` | data → text | SSOT содержимого ячейки; placeholders = SQL columns |
| `label` | presentation | заголовок секции inspect / list |
| `format` | presentation | `list` \| `inline` (default `list`, если задан template) |
| `split` | presentation | разделитель элементов list после render (default `, `) |

**Shorthand:** `source = peak_users_by_host` ≡ `template = "{peak_users_by_host}"`.  
`source` и `template` вместе — parse error, если не эквивалентны.

Регистрация:

- `!include "tooltips/*.dashtooltip"` на tab/dashboard;
- или `include tooltip "…"` внутри `@diagram` module;
- или inline `tooltip <id> … end tooltip` в diagram module.

### 3. Wiring: `inspect` на diagram / card

```text
@diagram lus_peak_concurrent_heatmap

include tooltip "../../tooltips/peak-hosts.dashtooltip"

chrome
  use heatmap_tall
end chrome

heatmap
  x = usage_date
  y = app_name
  value = peak_concurrent_proxy
  # no tooltip_* here
end heatmap

inspect
  use tooltip peak_hosts
end inspect
```

На card (override):

```text
card peak {
  inspect { use tooltip peak_hosts }
  on click {
    show below as list from tooltip
  }
  diagram lus_peak_concurrent_heatmap
  …
}
```

`from tooltip` без id → **inspect tooltip** карточки/диаграммы.  
`from tooltip peak_hosts` → явная ссылка (на будущее; v1 достаточно default).

Merge: diagram-module inspect → card inspect override (card wins).

### 4. Template grammar (a-la CIDE `presentation`)

Паттерн как у [CIDE ADR-0017](../cascade-ide/docs/adr/0017-multi-window-workspace-and-agent-surfaces.md): строка + **настраиваемые токены грамматики** + EBNF в ADR; реализация — узкий ручной парсер + тесты (не ANTLR, пока DSL узкий — см. CIDE ADR-0032).

#### 4.1 Default tokens

| Token | Default | Смысл |
|-------|---------|--------|
| `open_placeholder` / `close_placeholder` | `{` / `}` | границы имени колонки |
| `escape` | `\\` | экранирование следующего символа (в т.ч. `{`, `}`, `\\`) |
| `newline_escape` | `\n` | литеральный перевод строки в template string |

Опциональная секция на `@tooltip` (или `@tooltipgrammar` / stdlib preset — follow-up, если понадобится менять маркеры без правки шаблонов):

```text
@tooltip peak_hosts

grammar
  open = "{"
  close = "}"
  escape = "\\"
end grammar

template = "{peak_bucket_hhmm}\n{peak_users_by_host}"
…
```

v1: секция `grammar` **опциональна**; без неё — defaults выше. Менять `open`/`close` на `[`/`]` допустимо (тест + один stdlib preset), но **не** требуется для LUS.

#### 4.2 EBNF (template body)

```ebnf
template   ::= { fragment }
fragment   ::= literal | placeholder | escape_seq | newline_esc
placeholder ::= OPEN ident CLOSE
literal    ::= any char except OPEN, ESCAPE-prefix, and the two-char newline_esc
escape_seq ::= ESCAPE ( OPEN | CLOSE | ESCAPE | "n" )
newline_esc ::= "\\" "n"     (* when ESCAPE is backslash — sugar; same as escape_seq with n *)
ident      ::= (letter | "_") { letter | digit | "_" }
OPEN/CLOSE/ESCAPE ::= from grammar tokens (defaults "{", "}", "\\")
```

Семантика render (одна SQL row):

1. Каждый `placeholder` → `FormatCell(row[ident])` (тот же formatter, что heatmap labels: null/DBNull → `""`).
2. Склейка fragments → raw tooltip string ячейки.
3. Если результат `IsNullOrWhiteSpace` → ячейка **без** tooltip payload (`null` в matrix), UI не показывает inspect.
4. `format = list`: Host/Presenter сплитает **body** по `split` (как сейчас); если raw содержит `\n`, первая линия может быть headline (peak time) — **только** если author так заложил в template (`"{time}\n{list}"`), не магия Core.

#### 4.3 SELECT columns

`DiagramBindings.SelectedSqlColumns` / query builder:

- всегда: measure roles (`x`/`y`/`value`/…);
- плюс **все** `ident` из resolved tooltip template (и time больше не отдельное свойство).

### 5. Pointer intents (unchanged from ADR-0028)

| Intent | Trigger | Spec |
|--------|---------|------|
| Inspect | hover | resolved tooltip → popover / hover-detail |
| Select + show | click | `on click { show … from tooltip\|cell }` |
| Navigate | click | `set` / `goto` |

Отдельный `on hover` **не** вводим.

### 6. Deprecated (transitional, one release)

| Было | Стало |
|------|--------|
| `tooltip = col as "Label"` в `heatmap { }` | `@tooltip` + `template`/`source` + `label` + `inspect { use tooltip … }` |
| `tooltip_time = col` | `{col}` в `template` |
| `tooltip_format` / `tooltip_split` в heatmap | поля `@tooltip` |
| Core `FormatCellTooltip` time+`\n`+body | render template |

Parser v1: legacy heatmap props **ещё парсятся** → синтетический anonymous tooltip (`id = "__legacy"`) + warning/lint. Следующий minor: parse error.

### 7. LUS examples (target authoring)

**`tooltips/peak-hosts.dashtooltip`:**

```text
@tooltip peak_hosts

label = "Одновременно в пике"
format = list
split = "; "
template = "{peak_bucket_hhmm}\n{peak_users_by_host}"
```

**`tooltips/peak-apps.dashtooltip`:**

```text
@tooltip peak_apps

label = "Состав в пике"
format = list
split = ", "
source = peak_apps
```

**`diagrams/overview/peak-concurrent-heatmap.dashdiagram`:** heatmap только measure + `inspect { use tooltip peak_hosts }`.

### 8. Pipeline

1. Parse/register `@tooltip` → `TooltipDefinition`.
2. Resolve card: attach effective `TooltipDefinition` (inspect ref).
3. Query: SELECT includes placeholder columns.
4. Payload: per cell render template → `MatrixPayload.Tooltips` (raw string or null).
5. `MatrixPresentation`: label/format/split from tooltip entity (не из diagram props).
6. Host viz / `CardSelectionPresenter`: без специальной time-логики; headline из первой линии только если template её положил.

Validate:

- `show … from tooltip` ⇒ card has resolved tooltip;
- `inspect { use tooltip X }` ⇒ `X` registered;
- unknown placeholder ident ⇒ parse/resolve error (fail closed).

## Non-goals (v1)

- expressions / filters в template (`{col|upper}`, тернарники, join);
- `on hover` behaviour block;
- Chart.js native tooltip для matrix-canvas (остаётся Host inspect surface);
- генератор парсера из EBNF / ANTLR;
- отдельный `.dashbehaviour` для tooltip.

## Consequences

- **Core:** `TooltipDefinition`, `TooltipModuleParser`, include kind `tooltip` / `.dashtooltip`, template lexer/renderer, resolve + semantic validate; deprecate heatmap tooltip props.
- **Host:** `MatrixPresentation.FromCard` читает entity; убрать ad-hoc time split из payload builder.
- **LUS:** вынести tooltip в `.dashtooltip`; heatmap modules — measure-only + inspect.
- **Amends** [ADR-0004](DASHSPEC-ADR-0004-diagram-column-as.md): `tooltip` column binding на heatmap deprecated.
- **Amends** [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md): `from tooltip` означает entity, не diagram property.
- **Replaces** прежний фокус этого ADR («только вынести format/label в presentation.inspect»).

## Implementation plan (after Accept)

1. ADR review → Status **Accepted**.
2. Model + parser + `!include` / `include tooltip` + inline block.
3. Template grammar + renderer + tests (incl. escape / empty → null).
4. Resolve/validate wiring; legacy synthesizer.
5. Host presentation path; remove Core time-merge magic.
6. Migrate LUS heatmaps; soak Host.

## Open questions

- Нужен ли v1 отдельный `@tooltipgrammar` / stdlib preset, или достаточно optional `grammar { }` внутри tooltip (как локальный `[presentation_grammar]`)?
- Headline convention: оставить «первая линия до `\n` = peak time» в Presenter как **документированный** contract template shape, или ввести явный `headline_template` позже?
