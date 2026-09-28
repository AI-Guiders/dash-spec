# DASHSPEC-ADR-0059: Vertical viz render plugins

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-28 |
| **Relates to** | [ADR-0008](DASHSPEC-ADR-0008-viz-render-plugins.md), [ADR-0013](DASHSPEC-ADR-0013-host-solid-ports-viz-registry.md), [ADR-0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md) |

## Context

Built-in diagram renderers (Chart.js, matrix-canvas, table, …) lived inside `DashSpec.Host`: Razor components, static JS/CSS, and matrix toolbar toggles in `DashboardCardView`. That broke the microkernel model from ADR-0033 and made heatmap fixes fragile (host “knew” matrix UX).

## Decision

1. **`DashSpec.Viz`** — host↔plugin contract: `CardRenderResult`, `CardVizRenderContext`, `CardActionRequest`, DOM id helpers, chart height helper, `IOnClickInteractionService`.
2. **`plugins/viz/DashSpec.Plugin.Viz.Builtins`** — Razor class library; one **`IDashSpecPlugin` per `render` id** (`viz_matrix_canvas`, `viz_chartjs`, …). Each plugin registers:
   - `IVizPlugin` backend (data-family fallback),
   - `VizRendererDescriptor`,
   - card viz `IComponent` via `RegisterCardVizComponent`,
   - optional card toolbar via `RegisterVizCardToolbar` (matrix label toggles).
3. **Host** — `CardVisualization` remains the slot; `DynamicComponent` + registries built from contributor registry after plugin load. Card chrome toolbar dispatches viz-owned toolbar components. Static assets served from `_content/DashSpec.Plugin.Viz.Builtins/`.
4. **Execution unchanged** — payloads still built in `DashSpec.Execution.Runtime`; only presentation moves to plugins.

## Consequences

- New renderer: add vertical slice under `plugins/viz/` (or product DLL later with `[[viz.load]]`), register contributors; no Host `switch`.
- Matrix UX (values / X labels) ships with matrix render plugins, not host card view.
- **Next:** optional `[[viz.load]]` DLL staging for product-only renderers (same pattern as extension plugins).
- **Quality:** `node --check` on `wwwroot/js/*.js` at build; Playwright E2E (`tests/DashSpec.Host.E2E`) fails on `console.error`, `pageerror`, and failed script loads — see [docs/E2E.md](../docs/E2E.md).
