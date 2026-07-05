# DASHSPEC-ADR-0031: Display vocabulary — remove `as`

| | |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-07-05 |
| **Relates to** | [ADR-0004](DASHSPEC-ADR-0004-diagram-column-as.md), [ADR-0023](DASHSPEC-ADR-0023-dashcatalog.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md), [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md), [ADR-0030](DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md) |

## Context

`as` пришёл из PlantUML как **alias** (`participant "Name" as A`). В DashSpec им размазали:

- catalog / tab / card / page titles;
- column и filter labels;
- gate messages;
- effect formats (`show below as list`).

Один токен — разные роли; один и тот же title («№1 …») дублируется на **entry → report → page → card**. Автор не видит, какая строка где в UI.

**Explicit > implicit:** layer задаёт **тип блока**, не отдельное ключевое слово на каждый synonym (`heading` / `caption` / …).

## Decision

### Удалить `as` из grammar

Parse error на `… as "…"` после migration window. Не alias-сugar.

### Vocabulary (3 property keys + show phrase)

| Key | Applies to | Meaning |
|-----|------------|---------|
| **`title`** | `group`, `entry`, `tab`, `report`, `page`, `card` | human name **именованной сущности** |
| **`label`** | `filter`, diagram axis/column binding | подпись **поля данных** |
| **`message`** | `gate` | текст empty-state / placeholder |

**`show`** — отдельная grammar у `on click` ([ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md)), без meta-key `format` / `as`.

Нет `heading`, `caption`, `as`, `format`.

### `title` — one word, block defines placement

Host maps by **container block**:

| Block | `title` in UI |
|-------|----------------|
| `group` | catalog picker section |
| `entry` | catalog picker item + default report header |
| `tab` | tab label (embed / `@dashboard`) |
| `report` | page header (optional override) |
| `page` | sub-nav under tab |
| `card` | card chrome (optional) |

```text
group stakeholder {
  title = "Заказчик"

  entry peak_by_app {
    title = "№1 Пик одновременности по ПО"
    dashspec = "lus-stakeholder-peak-by-app.dashspec"
  }
}

@tab peak_by_app {
  report {
    page default {
      card peak_by_app {
        diagram lus_stakeholder_peak_by_app_bar
        bind period_grain, period_start, app_name
      }
    }
  }
}
```

#### Prod 1:1 entry convention

Standalone catalog entry:

- **`title` only on `entry`** (canonical product name);
- inner `@tab` / `page` / `card` **id = entry id**, **`title` omitted**;
- Host: header + picker from entry; card chrome без второго заголовка.

Embed / dev soak (`@dashboard` + `tab { dashspec … }`): `tab { title = "…" }`; cards may set `title` when id ≠ entry.

### `label` — data fields only

```text
filter date usage_date {
  column = usage_date
  label = "Дата отчёта"
  default = -7d..today
}

filter top events_top {
  label = "Строк (TOP)"
  default = 200
}

diagram heatmap {
  x = usage_date
  x_label = "День"
  y = user_sam
  y_label = "Пользователь"
  value = peak_concurrent_apps
  value_label = "Разных ПО"
  tooltip = peak_apps
  tooltip_label = "Состав в пике"
}
```

Shorthand (optional v1.1): `filter date usage_date on usage_date label "Дата"` — без block.

IR: `{key}_label` in diagram properties (rename from `{key}_as`).

### `message` — gate

```text
card user_day_heatmap {
  gate requires user_name {
    message = "Выберите пользователя или кликните bar выше"
  }
  diagram …
}
```

```text
gate when user_name.empty   # no message — browse card simply hidden when set
```

### `show` — placement + variant (not `format`)

Replaces `show below as list`. Grammar:

```text
show <placement> <variant> data from <source> [copy]
```

| Slot | Whitelist v1 | Notes |
|------|--------------|-------|
| **placement** | `below` | **implemented** |
| **placement** | `above`, `left`, `right` | parse + host follow-up ([ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md)) |
| **variant** | `list`, `kv`, `plain` | shape of detail strip |
| **source** | `tooltip`, `cell` | after **`data from`** |
| **copy** | optional | selectable UI |

```text
on click {
  show below list data from tooltip copy
  show right kv data from cell
}
```

**`data from`** — что показать. **`set … from x`** — куда записать filter. Разные конструкции.

**Placement** — card body anchor относительно **viz slot** (diagram + legend), не clicked cell. См. [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md).

| placement | Meaning |
|-----------|---------|
| `below` / `above` | strip под/над viz (inside card) |
| `left` / `right` | split `[ selection \| viz ]` / `[ viz \| selection ]` |

Cell-adjacent hover = inspect (ADR-0029).

Block form (optional sugar):

```text
show {
  placement = below
  list data from tooltip copy
}
```

## Migration

| Legacy | Target |
|--------|--------|
| `entry id as "T"` | `entry id { title = "T"; … }` |
| `group id as "T"` | `group id { title = "T"; … }` |
| `tab id as "T"` | `tab id { title = "T"; … }` |
| `card id as "T"` | `card id { title = "T"; … }` or omit (entry owns) |
| `page id as "T"` | `page id { title = "T"; … }` |
| `report "T" { }` | `report { title = "T"; … }` |
| `col as "L"` | `col` + `col_label = "L"` or `label = "L"` in filter block |
| `filter … on col as "L"` | `filter … { column = col; label = "L" }` |
| `gate requires f as "msg"` | `gate requires f { message = "msg" }` |
| `show below as list` | `show below list data from tooltip` |
| `show below as kv` | `show below kv data from cell` |
| `show below list from tooltip` | `show below list data from tooltip` (alias one release) |

Mechanical rewrite in LUS `docs/dashspec/` — follow-up PR after parser.

## Parser / IR

- Remove `As` token path for display strings.
- `CatalogEntryDefinition.Title` ← `entry { title = … }`.
- `CardDefinition.Title` ← `card { title = … }` (optional).
- `FilterDefinition.Label` ← `label =` only.
- Diagram: `x_label`, `y_label`, … (alias `x_as` deprecated one release).

## Non-goals

- Rename entity `id` identifiers.
- `title` on diagram kinds (use `label` on bindings).
- i18n / locale keys.

## Consequences

- **ADR-0004** amended — `column as "Label"` deprecated → `label` / `{key}_label`.
- **ADR-0028** — `show <placement> <variant> from …`; placement whitelist.
- **ADR-0030** examples use this vocabulary.
- **Authors:** prod reports — one `title` on entry; soak — `title` on tab/card as needed.
- **Host:** title inheritance entry → report header; suppress duplicate card title when ids match.

## Relation to PlantUML

PlantUML `as` = diagram **alias**. DashSpec не рисует одну static diagram — **не переносим `as`**. Display = explicit properties on blocks.
