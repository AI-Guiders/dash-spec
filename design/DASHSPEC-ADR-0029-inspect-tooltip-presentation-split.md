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
| **Tooltip** | `@tooltip` / `.dashtooltip` | `variables` + `tooltip = "…"`, `label`, `format`, `split` |
| **Inspect / act** | `inspect` + `on click` | hover popover; `show below list from tooltip` |

- **Нет блока tooltip** → value-only heatmap: hover-detail только по value (без list/inspect UX), `from tooltip` в card — **ошибка resolve**.
- **Есть tooltip, ячейка после render пустая** → нет hover inspect / пустой list (не «Нет данных для выбранной ячейки»).
- Warm stub без drill-down колонок → **не объявлять** tooltip в spec (не маскировать NULL в SQL «для совместимости»).

### 2. Tooltip — first-class module + `variables`

Канон в духе CIDE `presentation` / `[presentation_grammar]`: **сначала именованные слоты**, потом строка, которая на них ссылается. Placeholders в строке — **не** сырые SQL-имена, а ключи из `variables`.

Файл `.dashtooltip`:

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

Минимальный одноколоночный:

```text
@tooltip peak_apps

variables
  apps = peak_apps
end variables

tooltip = "{apps}"
label = "Состав в пике"
format = list
split = ", "
```

| Поле / блок | Слой | Смысл |
|-------------|------|--------|
| `variables` | binding | `name = column` — слот → SQL column (v1); имя слота — идентиф. для placeholders |
| `tooltip` | data → text | interpolated string; `{name}` только из `variables` |
| `label` | presentation | заголовок секции inspect / list |
| `format` | presentation | `list` \| `inline` (default `list`, если задан `tooltip`) |
| `split` | presentation | разделитель элементов list после render (default `, `) |

**Правила v1:**

- `variables` обязателен, если есть `tooltip =` с хотя бы одним placeholder.
- `{unknown}` (нет в `variables`) → **parse/resolve error** (fail closed).
- Слот объявлен, но не использован в `tooltip` → warning/lint (SELECT всё равно может не тянуть лишнее: в SELECT только слоты, реально встречающиеся в строке).
- RHS `variables`: v1 = **column binding** (ident). Строковый литерал (`prefix = "пик "`) — follow-up, не блокирует LUS.
- Alias свойства: `template =` ≡ `tooltip =` (одно имя канон — `tooltip`; `template` deprecated synonym one release).

**Shorthand:** `source = peak_apps` ≡

```text
variables
  value = peak_apps
end variables
tooltip = "{value}"
```

(`source` + явные `variables`/`tooltip` вместе — parse error.)

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

Два уровня, как у CIDE:

1. **Словарь слотов** — блок `variables` (аналог настраиваемых идентификаторов / `[presentation_grammar]` literals).
2. **Строка раскладки** — `tooltip = "…"` (аналог строки `presentation`), EBNF ниже.

Реализация — узкий ручной парсер + тесты (не ANTLR, пока DSL узкий — см. CIDE ADR-0032).

#### 4.1 Placeholder markers (optional `grammar`)

| Token | Default | Смысл |
|-------|---------|--------|
| `open` / `close` | `{` / `}` | границы **имени слота** (не SQL column) |
| `escape` | `\\` | экранирование `{`, `}`, `\\`, `n` |

```text
@tooltip peak_hosts

grammar
  open = "{"
  close = "}"
  escape = "\\"
end grammar

variables
  peak_time = peak_bucket_hhmm
  hosts = peak_users_by_host
end variables

tooltip = "{peak_time}\n{hosts}"
```

v1: `grammar` опционален (defaults). Отдельный `@tooltipgrammar` / stdlib — **не** нужен, пока хватает локального блока.

#### 4.2 EBNF

```ebnf
tooltip_module ::= { variables_block | grammar_block | prop }

variables_block ::= "variables" { binding } "end" "variables"
binding         ::= ident "=" column_ident

grammar_block   ::= "grammar" { grammar_prop } "end" "grammar"

prop            ::= "tooltip" "=" string
                  | "label" "=" string
                  | "format" "=" ("list" | "inline")
                  | "split" "=" string
                  | "source" "=" column_ident   (* shorthand *)

tooltip_string  ::= { fragment }
fragment        ::= literal | placeholder | escape_seq
placeholder     ::= OPEN slot_ident CLOSE
slot_ident      ::= ident   (* must exist in variables *)
literal         ::= any char except OPEN and ESCAPE-prefix
escape_seq      ::= ESCAPE ( OPEN | CLOSE | ESCAPE | "n" )
OPEN/CLOSE/ESCAPE ::= from grammar tokens
```

#### 4.3 Render (одна SQL row)

1. Разобрать `tooltip` string → fragments.
2. Каждый `placeholder` `{slot}` → `FormatCell(row[variables[slot]])` (null/DBNull → `""`).
3. Склейка → raw string ячейки.
4. `IsNullOrWhiteSpace` → `null` в matrix (нет inspect UX).
5. `format = list`: Presenter сплитает body по `split`. Форма `"{peak_time}\n{hosts}"` — **документированный** shape для headline (первая линия), не магия Core по имени колонки.

#### 4.4 SELECT columns

В SELECT попадают **RHS** слотов, которые реально встречаются в `tooltip` string (не весь `variables` block).

Measure roles (`x`/`y`/`value`) — по-прежнему с diagram; tooltip columns — только через entity.

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
| `tooltip = col as "Label"` в `heatmap { }` | `@tooltip` + `variables` + `tooltip = "{slot}"` + `label` + `inspect { use tooltip … }` |
| `tooltip_time = col` | слот в `variables` + `{slot}` в строке |
| `tooltip_format` / `tooltip_split` в heatmap | поля `@tooltip` |
| Core `FormatCellTooltip` time+`\n`+body | render `tooltip` string через `variables` |
| placeholders = SQL column names | placeholders = variable names only |

Parser v1: legacy heatmap props **ещё парсятся** → синтетический anonymous tooltip (`id = "__legacy"`) + warning/lint. Следующий minor: parse error.

### 7. LUS examples (target authoring)

**`tooltips/peak-hosts.dashtooltip`:**

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

**`tooltips/peak-apps.dashtooltip`:**

```text
@tooltip peak_apps

variables
  apps = peak_apps
end variables

tooltip = "{apps}"
label = "Состав в пике"
format = list
split = ", "
```

**`diagrams/overview/peak-concurrent-heatmap.dashdiagram`:** heatmap только measure + `inspect { use tooltip peak_hosts }`.

### 8. Pipeline

1. Parse/register `@tooltip` → `TooltipDefinition` (`Variables` map + `Tooltip` string).
2. Resolve card: attach effective `TooltipDefinition` (inspect ref).
3. Query: SELECT includes RHS columns for slots used in `tooltip` string.
4. Payload: per cell resolve slots → render string → `MatrixPayload.Tooltips` (raw or null).
5. `MatrixPresentation`: label/format/split from tooltip entity.
6. Host viz / `CardSelectionPresenter`: headline из первой линии только по documented template shape.

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
3. `variables` + `tooltip` string grammar/renderer + tests (unknown slot → error; empty → null).
4. Resolve/validate wiring; legacy synthesizer.
5. Host presentation path; remove Core time-merge magic.
6. Migrate LUS heatmaps; soak Host.

## Open questions

- Headline: оставить documented shape `"{peak_time}\n{hosts}"` (первая линия = headline в Presenter), или позже явный `headline = "{peak_time}"`?
- Literals в `variables` (`prefix = "пик "`) — в том же minor, что Accept, или follow-up?
