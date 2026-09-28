# DASHSPEC-ADR-0062: Viz axis presentation (diagram-agnostic)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-28 |
| **Relates to** | [ADR-0059](DASHSPEC-ADR-0059-vertical-viz-plugins.md), [ADR-0002](DASHSPEC-ADR-0002-layout-and-presentation.md), [ADR-0003](DASHSPEC-ADR-0003-diagram-kinds.md) |

## Context

Heatmap had matrix-specific names (`ICardMatrixDisplayState`, `toggle_matrix_*`, `MatrixYOrderParser`). Bar/line/table share the same ideas (category axis order, axis/value label visibility) but no shared contract. Operators expect one mental model: spec defaults + session toggles, renderer implements drawing.

## Decision

### 1. Core — `VizAxisPresentation` + `CategoryAxisOrder`

- **Spec parsing** (diagram properties, any kind): `value_labels`, `axis_labels_x` / `axis_labels_y`, `toolbar_*`, `y_sort` / `y_order` / `y_format` (and `x_*` aliases when added).
- **`CategoryAxisOrdering`**: sort a category label list by **label** or **aggregate value**; default mode inferred from axis `*_format` (`raw` → label asc; `user.*` / `date.*` → value desc) unless `*_sort` overrides.
- **`VizLabelDisplayResolver`**: merge spec + session override for value/cell labels and axis labels.

Diagram kind does **not** own this logic; payload builders call `CategoryAxisOrdering` when they expose a category axis.

### 2. Session — `ICardVizDisplayState`

Per-card overrides for value labels and axis X/Y (nullable = follow spec). Replaces `ICardMatrixDisplayState`. Host registers one scoped service.

### 3. Actions — `VizCardDisplayActions`

Canonical action ids:

- `toggle_viz_value_labels`
- `toggle_viz_axis_labels_x`
- `toggle_viz_axis_labels_y`

Legacy `toggle_matrix_*` ids normalize to the same handler (catalog / old clients).

### 4. Host

- `CardVizDisplayToggle.TryApply` in `DashSpec.Viz` applies toggles when `CardRenderResult` exposes `IVizAxisPresentationSpec` (today: `MatrixPresentation` on matrix payloads).
- Command context **`VizToolbarCards`** (was `MatrixCards`): cards eligible for viz display slash commands (heatmap family v1).
- `DashboardCardView.ShouldRender` includes viz display override snapshot.

### 5. Viz plugins

- Toolbar components dispatch `VizCardDisplayActions`; renderers read `ICardVizDisplayState` + spec (matrix-canvas, css-grid heatmap).
- Chart.js bar: category order via existing `order_by` / transforms until a future slice maps `CategoryAxisOrder` to chart payloads.

## Non-goals (this ADR)

- Unified grammar block `axis { … }` replacing heatmap properties (aliases only).
- Chart.js tick toggles for non-matrix charts (hook is `IVizAxisPresentationSpec`).

## Consequences

- Matrix-only types removed from public surface; tests renamed to `CategoryAxisOrder` / `CardVizDisplayState`.
- New renderers opt in: implement presentation spec + toolbar registration, call shared ordering when building categories.
