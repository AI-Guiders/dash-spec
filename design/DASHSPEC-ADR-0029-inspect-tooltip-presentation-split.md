# DASHSPEC-ADR-0029: Inspect (tooltip) vs diagram data bindings

| | |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-07-05 |
| **Relates to** | [ADR-0004](DASHSPEC-ADR-0004-diagram-column-as.md), [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md), [ADR-0012](DASHSPEC-ADR-0012-host-presentation-layering.md), [ADR-0027](DASHSPEC-ADR-0027-single-declaration-and-layout-ids.md) |

## Context

Heatmap tooltip сегодня размазан по трём слоям:

1. **Spec / diagram** — `tooltip = peak_apps as "Состав в пике"`, плюс `tooltip_format`, `tooltip_split` в `diagram.Properties` ([ADR-0004](DASHSPEC-ADR-0004-diagram-column-as.md)).
2. **Core** — `MatrixPayloadBuilder` склеивает и merge-ит **display strings** в payload ([`MatrixPayloadBuilder.cs`](../src/DashSpec.Core/Runtime/MatrixPayloadBuilder.cs)).
3. **Host** — `HeatmapView` рисует popover на `@onmouseenter` ad hoc ([`HeatmapView.razor`](../src/DashSpec.Host/Components/Charts/HeatmapView.razor)).

Подпись колонки, формат списка и hover-UI — **presentation**, но живут рядом с SQL binding. [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md) уже отделяет `diagram` (data) от `presentation` (chrome) для line/bar — heatmap tooltip не довели до того же канона.

Отдельно планируются **actions** (`on click` → `goto tab`, `set filter`) — [follow-up ADR actions]. Hover для popover и click для drill-down — **разные intent**, но один DOM event surface; смешивать их в одном «behaviour mini-PL» не нужно.

## Decision

### 1. Два intent на pointer events

| Intent | Событие | Слой | Пример |
|--------|---------|------|--------|
| **inspect** | hover (focus) | presentation + viz | popover «Состав в пике» |
| **act** | click | behaviour (отдельный ADR) | `goto tab detail`, `set user_name from y` |

**inspect** — read-only, без side effects на filter state.  
**act** — mutation / navigation.  

Default: если задан `inspect.tooltip`, viz показывает popover on hover; отдельный `on hover` в behaviour **не вводим**.

### 2. Data vs inspect в spec

**Diagram** — только **data binding** (колонка в SELECT):

```text
heatmap {
  x = usage_date as "День"
  y = user_sam as "Пользователь"
  value = peak_concurrent_apps as "Разных ПО"
  tooltip = peak_apps
}
```

- `tooltip = <column>` — optional column binding, как `x`/`y`/`value`.
- **`as "…"` на `tooltip`** — deprecated; label → `inspect`.

**Presentation** — chrome + inspect:

```text
presentation {
  use = heatmap_tall
  inspect {
    tooltip {
      label = "Состав в пике"
      format = list
      split = ", "
    }
  }
}
```

| Поле | Слой | Смысл |
|------|------|--------|
| `tooltip = peak_apps` | diagram | колонка данных |
| `inspect.tooltip.label` | presentation | заголовок секции popover |
| `inspect.tooltip.format` | presentation | `list` \| `inline` |
| `inspect.tooltip.split` | presentation | split для list (default `, `) |

Порядок merge ([ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md)): **`.dashpresentation` preset → inline `presentation` → deprecated diagram props → defaults**.

### 3. Файл `.dashpresentation`

Расширяем module (flat properties + nested blocks):

```text
@presentation heatmap_stakeholder_tooltip

inspect {
  tooltip {
    label = "Состав в пике"
    format = list
    split = ", "
  }
}
```

На `@diagram` module:

```text
@diagram lus_stakeholder_peak_apps_heatmap

include presentation "<presentation/heatmap_tall>"
include presentation "presentations/stakeholder-peak-apps-tooltip.dashpresentation"

heatmap {
  …
  tooltip = peak_apps
}
```

`height` остаётся в presentation preset ([`heatmap_tall.dashpresentation`](../src/DashSpec.Core/stdlib/presentation/heatmap_tall.dashpresentation)); tooltip inspect — отдельный preset или inline.

### 4. Core / Host pipeline

**До (сейчас):** SQL rows → `MatrixPayloadBuilder` форматирует tooltip **strings** → Host только показывает.

**После:**

1. **Query** — SELECT включает tooltip column, если `diagram` declares `tooltip`.
2. **Payload** — `MatrixPayload.Tooltips` хранит **raw cell values** (или `string[]` после split на data boundary), не финальный UI text.
3. **`InspectPresentation`** (расширение `MatrixPresentation`) — label, format, split, axis formats, color scale.
4. **Viz plugin** — `OnInspect(hoverContext)` рендерит popover; heatmap/bar/table единый контракт inspect.

`MatrixPresentation.FromCard` читает `inspect.tooltip.*` из merged presentation, **не** `tooltip_format` из diagram.

### 5. Card-level override

На card (optional):

```text
card peak_apps {
  title = "№2 …"
  presentation {
    inspect {
      tooltip { label = "Состав в пике" format = list }
    }
  }
  diagram lus_stakeholder_peak_apps_heatmap
  …
}
```

Merge: diagram-module presentation → card presentation override.

### 6. Deprecated (transitional)

| Было | Стало |
|------|--------|
| `tooltip = col as "Label"` | `tooltip = col` + `inspect.tooltip.label` |
| `tooltip_format` / `tooltip_split` в `heatmap { }` | `presentation.inspect.tooltip` |
| pre-merged strings в payload | raw values + inspect render |

Parser v1: deprecated form still parses; lint warns.

### 7. Relation to actions (follow-up)

```text
card peak_apps {
  title = "№2 …"
  presentation {
    inspect { tooltip { label = "Состав в пике" format = list } }
  }
  on click {
    set usage_date from x
    set user_name from y
    goto tab detail
  }
  diagram lus_stakeholder_peak_apps_heatmap
  …
}
```

- hover → inspect (viz)
- click → behaviour
- **не** `.dashbehaviour` для tooltip v1

## Example (LUS stakeholder heatmap)

**`presentations/stakeholder-peak-apps-tooltip.dashpresentation`:**

```text
@presentation stakeholder_peak_apps_tooltip

inspect {
  tooltip {
    label = "Состав в пике"
    format = list
    split = ", "
  }
}
```

**`diagrams/stakeholder/stakeholder-peak-apps-heatmap.dashdiagram`:**

```text
@diagram lus_stakeholder_peak_apps_heatmap

include presentation "<presentation/heatmap_tall>"
include presentation "../../presentations/stakeholder-peak-apps-tooltip.dashpresentation"

heatmap {
  render = "css-grid"
  x = usage_date as "День"
  y = user_sam as "Пользователь"
  value = peak_concurrent_apps as "Разных ПО"
  tooltip = peak_apps
  x_format = date.short
  y_format = user.short
  color_scale = heat
}
```

Follow-up: `x_format`, `y_format`, `color_scale` → `presentation` / inspect axes (не блокирует tooltip split).

## Non-goals

- expressions / templates в tooltip (`{user}` кроме legend-style `{max}` на card)
- `on hover` в behaviour для navigation
- `.dashbehaviour` file v1
- Chart.js tooltip для heatmap (остаётся css-grid viz или unified inspect port)

## Consequences

- **Core:** `InspectPresentation` / extend `PresentationBlock`; `PropertySchemas` + nested `inspect`; payload raw tooltips.
- **Host:** `HeatmapView` → inspect через viz API; меньше string logic в Core builder.
- **LUS:** вынести tooltip label/format из `.dashdiagram` в `.dashpresentation`.
- **Amends [ADR-0004](DASHSPEC-ADR-0004-diagram-column-as.md):** `as` на `tooltip` deprecated; label в inspect.

## Implementation plan

1. Schema `presentation.inspect.tooltip` + parser (card, diagram module, `@presentation` file).
2. `MatrixPresentation` / merge из presentation blocks.
3. Payload builder: raw tooltip values; move format/split to Host inspect.
4. Lint: warn on diagram `tooltip_format` / `tooltip as`.
5. Migrate LUS stakeholder heatmaps.
