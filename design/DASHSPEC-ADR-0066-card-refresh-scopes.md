---
type: ADR
id: DASHSPEC-ADR-0066
title: "Host refresh scopes (implementation of ADR-0028)"
status: "Accepted"
date: 2026-09-29
tags: [dashspec, adr]
language: ru
relates:
  - DASHSPEC-ADR-0064
  - DASHSPEC-ADR-0065
---

# DASHSPEC-ADR-0066: Host refresh scopes (implementation of ADR-0028)

## Context

`DashboardRefreshCoordinator` used to always run **CardFull** refresh (Loading + Busy + primary SQL).

Parsed click effects already imply different refresh semantics ([ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md)). Core exposes `CardClickRefreshPlanner` → `CardInteractionRefresh`. This ADR names **how Host maps that enum to coordinator methods** — not author workflow.

## Decision

| `CardInteractionRefresh` (Core) | Coordinator | `Card.Loading` / `Busy` |
|---------------------------------|-------------|-------------------------|
| `Report` | `RefreshDashboardAsync` / `ApplyFilters` path | yes |
| `CardInteriorSlots` | `RefreshCardInteriorSlotsAsync` | **no** |
| `None` | — | no |

Additional Host-only scopes (not from `on click`):

| Trigger | Coordinator |
|---------|-------------|
| Local filter apply on card | `RefreshCardLocalAsync` |
| Plugin action `RefreshCard` | `RefreshSingleCardAsync` (**CardFull**) |

### Execution

- `CardInteriorSlots`: `ICardRenderer.RenderInteriorSlotsAsync` + `CardCellDrillOverlay` on drill slot compile only.

## Consequences

- New click effect: amend **ADR-0028** effect table + `CardClickRefreshPlanner`, then wire Host.
- Authors read [AUTHORING_GUIDE_RU](../docs/AUTHORING_GUIDE_RU.md) §6, not this ADR.
