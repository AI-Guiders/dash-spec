# DASHSPEC-ADR-0028: Bounded `on click` interactions

| | |
|---|---|
| **Status** | Accepted (v1 implemented; amended ADR-0031) |
| **Date** | 2026-07-05 |
| **Relates to** | [ADR-0029](DASHSPEC-ADR-0029-inspect-tooltip-presentation-split.md), [ADR-0027](DASHSPEC-ADR-0027-single-declaration-and-layout-ids.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md), [ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md) |

## Context

Интерактивность в DashSpec тянет в разные стороны:

- **inspect** (hover tooltip) — read-only;
- **select + present** (click → list/kv для copy) — sticky detail zone;
- **act** (click → set filter, goto tab) — navigation.

`.dashbehaviour`, CSX, `widgets { }` — риск mini-ЯП. Нужен **whitelist** effects на card.

## Decision

### Pointer intents (v1)

| Intent | Trigger | Spec |
|--------|---------|------|
| Inspect | hover | `presentation.inspect` ([ADR-0029](DASHSPEC-ADR-0029-inspect-tooltip-presentation-split.md)) |
| Select + show | click | `on click { show … }` |
| Navigate | click | `on click { set …; goto tab … }` |

Отдельный `on hover` в behaviour **не вводим** — hover = inspect/tooltip.

### Syntax (inline on card)

```text
card peak_apps {
  title = "№2 …"
  on click {
    show below list data from tooltip copy
  }
  diagram demo_stakeholder_peak_apps_heatmap
  …
}
```

```text
on click {
  show below kv data from cell copy
  set usage_date from x
  set user_name from y
  goto tab detail
}
```

### Whitelist effects (v1)

| Effect | Grammar | Что обновляется (семантика спеки) |
|--------|---------|-----------------------------------|
| **show** | `show <placement> <variant> data from <source> [copy]` | Только sticky detail в card body; **без** повторного SQL |
| **set** | `set <filter> from x\|y\|value` | **FilterState** отчёта → все карточки с `bind` на эти фильтры (как «Применить») |
| **drill table** | `drill table from cell` + `set <filter> from x\|y\|value` (≥1) | Только **non-primary** diagram slots этой карточки (`data for …`); primary diagram и toolbar **не** меняются |
| **goto** | `goto tab <id>` / `page` / `entry` | Навигация; фильтры по правилам `goto entry preserving` |

Host реализует таблицу через `CardClickRefreshPlanner` (Core); авторы **не** задают scope отдельным ключевом.

```text
on click
  drill table from cell
  set usage_date from x
  set app_name from y
end click
```

`set` внутри `drill table` — привязка ячейки к SQL **drill-слота**, не запись в toolbar.

#### `show` slots

```text
show <placement> <variant> data from <source> [copy]
```

| Slot | v1 parse | v1 host | follow-up |
|------|----------|---------|-----------|
| **placement** | `below` | ✅ strip under viz | `above`, `left`, `right` |
| **variant** | `list`, `kv`, `plain` | ✅ list/kv | — |
| **source** | `tooltip`, `cell` | ✅ | — |

**`data from`** — явная привязка источника (что показать). Не путать с:
- **`set … from x|y|value`** — запись в filter;
- **placement** — где показать (card body / viz).

**`from cell`** без `data` — deprecated alias one release.

#### Placement anchor — relative to what?

**Card** (spec `card { }`) = host `<article class="card">`:

```text
┌─ card (layout slot on tab board) ─────────────┐
│  card-head: title, local filters, chips       │
│  ┌─ viz slot (diagram + legend) ───────────┐  │
│  │  heatmap / chart / table                │  │
│  └─────────────────────────────────────────┘  │
│  selection strip (show below, v1)             │
└───────────────────────────────────────────────┘
```

- **Card ≠ diagram.** Diagram — `CardVisualization` внутри card. Title, filters, legend, selection — части card.
- **`from cell`** — **источник данных** (`data from cell`), **не** anchor placement.
- **Hover popover у ячейки** — inspect ([ADR-0029](DASHSPEC-ADR-0029-inspect-tooltip-presentation-split.md)), не `show left|right`.

**Placement** якорится к **card body**, относительно **viz slot** (область диаграммы + legend):

| placement | Meaning |
|-----------|---------|
| `below` | selection strip **под viz** (inside card, v1) |
| `above` | selection strip **между card-head и viz** |
| `left` | card body **split**: `[ selection \| viz ]` |
| `right` | card body **split**: `[ viz \| selection ]` |

Не tab-level side panel, не offset от clicked pixel. Split требует min-width / interior layout headroom — host may downgrade to `below` on narrow viewport (follow-up).

New placements only via amend this ADR.

- **Legacy:** `show below as list` / `show below list from tooltip` → `show below list data from tooltip`.
- **show list from tooltip:** split по `tooltip_split`; **copy** → selectable UI.
- **Order:** effects применяются сверху вниз; re-query по `CardInteractionRefresh` из Core (один scope на клик).

### Diagram kind → click target

Host infers cell/point/bar from diagram kind (heatmap → cell). Explicit `on cell click` — follow-up if multi-handler needed.

### Validation

- `set` filter must exist in resolved filter index.
- `goto tab` must exist on dashboard.
- `set` `from` field whitelist: `x`, `y`, `value` (not `data from` — that is `show` only).
- Duplicate `on click` on card → error.

## Examples

### demo stakeholder — copy list

```text
card stakeholder_peak_apps ref E {
  on click {
    show below list data from tooltip copy
  }
  diagram demo_stakeholder_peak_apps_heatmap
  …
}
```

### Drill-down (parse v1, host applies set + goto)

```text
on click {
  set usage_date from x
  set user_name from y
  goto tab detail
}
```

## Rejected (v1)

| Idea | Why |
|------|-----|
| `.dashbehaviour` file | inline достаточно |
| CSX / expressions | security + mini-PL |
| `widgets { }` registry | declarations smell |
| `on hover` actions | inspect layer |
| multi-column table widget | use detail tab / v2 |

## Consequences

- Core: `CardClickBehaviour`, `CardClickParser`, `CardDefinition.ClickBehaviour`
- Host: heatmap cell click → `CardSelectionDetail`; `DashboardPageController.ApplyCardClickNavigationAsync`
- demo: `stakeholder_peak_apps` — list copy v1

## Follow-up

- `card { title = "…" }` header ([ADR-0027](DASHSPEC-ADR-0027-single-declaration-and-layout-ids.md) direction)
- extract shared `on click` → `@behaviour` file when reuse > 2 cards
- `as table { column … }` selection widget v2
