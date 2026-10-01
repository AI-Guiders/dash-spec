# DASHSPEC-ADR-0072: Unified layout slot plane (host grid = card grid)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-01 |
| **Relates to** | [ADR-0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md), [ADR-0022](DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md), [ADR-0025](DASHSPEC-ADR-0025-card-interior-layout-board.md), [ADR-0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md), [ADR-0065](DASHSPEC-ADR-0065-card-interior-multi-slot.md) |

## Context

Authors and operators reason about **one idea**: a **grid of slots**; each slot holds a **typed component** that knows how to render (card shell, filter widget, diagram/table/matrix, nest, group). Host-level tab/page boards and card interior boards differ only in **scope** and **allowed content kinds**, not in placement math or CSS variables.

Today parse/layout **already share** bracket boards and `PlacementDefinition`. Host Blazor still branches: tab cards vs `DashboardFiltersSection` vs `DashboardCardView` interior `if/else` chains.

## Decision

### 1. Slot plane (canonical model)

| Concept | Meaning |
|---------|---------|
| **Scope** | Where the grid lives: `HostTabBoard`, `HostPageToolbar`, `CardInterior`, `CardFilterChrome` |
| **Token** | Cell ref from bracket board (`usage_date:2`, `main`, `drill`, card id) |
| **Placement** | `row / col / span` on a column grid |
| **Content kind** | What may occupy the slot: `Card`, `Nest`, `Group`, `Filter`, `PrimaryDiagram`, `SecondaryDiagram`, … |
| **Renderer** | Plugin or host shell that paints the slot; **does not** compile SQL |

**Invariant:** Host iterates `LayoutSlotDescriptor` ordered by placement; render dispatch is by `(Scope, ContentKind)` via registries — not ad-hoc Razor branches per feature.

### 2. Data vs display

| Layer | Responsibility |
|-------|----------------|
| **Parse / Core** | Boards, tokens, placements, binds, diagram slot map |
| **Execution** | `CardRenderService` builds payloads per diagram slot (`CardRenderResult`, `InteriorSlotRenders`) |
| **Host** | Materialize slot list → call filter/viz/card registries |
| **Plugins** | Viz + filter widgets; register by plugin id |

SQL and chart building stay **card/slot compile time** in Execution; the slot plane is **composition only**.

### 3. Migration phases

| Phase | Deliverable |
|-------|-------------|
| **M1** (now) | Core `LayoutSlotScope`, `LayoutSlotContentKind`, `LayoutSlotDescriptor`, `CardInteriorSlotPlan`; Host card interior uses plan + `switch` on kind |
| **M2** (done) | `LayoutSlotEngine` + `PlacementSlotGrid` / `PlacementSlot` (Presentation); `PlacementGridCss` host/interior variables; card interior on slot components |
| **M3** (done) | `DashboardFiltersSection` → `PlacementSlotGrid` + `LayoutSlotEngine.PlanHostPageToolbar` |
| **M4** (done) | `PlanHostTabBoard` / `PlanHostTabBoardCards`; `DashboardTabBoard` + `HostTabBoardSlot` (card / nest / group) |
| **M5** (done) | `ILayoutSlotRendererRegistry`; `CardInteriorSlotContent` + `HostTabBoardSlot` dispatch via registry |

### 4. Non-goals

- Replacing `CardDefinition` as the bind/click/chrome unit
- Moving query compilation into Blazor
- `nest` inside card interior (still out of scope per ADR-0065)

## Consequences

- New types in `DashSpec.Core.Layout`; tests on planners before UI refactors
- ADR-0065 «composition contract» is a **subset** of this ADR (card interior + plugins)
- Docs: author mental model = **two nested grids** (host → card), same slot rules
