# DASHSPEC-ADR-0066: Card refresh scopes (Host SSOT)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-29 |
| **Relates to** | [ADR-0064](DASHSPEC-ADR-0064-cell-drill-tabular-payload.md), [ADR-0065](DASHSPEC-ADR-0065-card-interior-multi-slot.md), [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md), [ADR-0012](DASHSPEC-ADR-0012-host-presentation-layering.md) |

## Context

`DashboardRefreshCoordinator` historically treated «обновить карточку» как **полный** re-render: `Card.Loading = true`, `Busy` на странице, повторный SQL по **primary** diagram и interior slots.

Для multi-slot cards ([ADR-0065](DASHSPEC-ADR-0065-card-interior-multi-slot.md)) и `drill table from cell` ([ADR-0064](DASHSPEC-ADR-0064-cell-drill-tabular-payload.md)) это неверно UX-ом: heatmap не должен мигать, тулбар не должен блокироваться, остальные карточки не трогаются.

Нужен **единый реестр** «какой click / apply → какой scope refresh», чтобы Host не дублировал ad-hoc правила в контроллерах.

## Decision

### 1. Refresh scopes (v1)

| Scope | Что перезапрашивается | `Card.Loading` | `Coordinator.Busy` | Другие карточки |
|-------|------------------------|----------------|--------------------|-----------------|
| **DashboardApply** | Все карточки, затронутые dashboard filters (или весь отчёт) | да (целевые) | да | да (целевые) |
| **CardFull** | Primary + все interior slots одной карточки | да | да | нет |
| **CardLocalApply** | Карточка + зависимости local filters / filter host | да (набор) | да | только зависимые |
| **CardInteriorSlots** | Только `InteriorSlotRenders` (non-primary slots) | **нет** | **нет** | нет |

**Правило:** scope выбирается по **effect**, не по виджету. Один клик — один scope.

### 2. Mapping: `on click` → scope

| Click effect | Filter / state | Refresh scope |
|--------------|----------------|---------------|
| `set <filter> from x\|y\|value` | Пишет **dashboard / toolbar** `FilterState` + `ApplyFilters` | **DashboardApply** (или **CardFull** если только local bind без toolbar — редко) |
| `drill table from cell` + `set …` | Пишет **per-card** `CardCellDrillOverlay` only | **CardInteriorSlots** |
| `show below …` | Локальный UI state в card view | **none** (без coordinator) |
| `goto tab` / `goto page` / catalog | Навигация + перенос фильтров | **DashboardApply** после load |
| Plugin `RefreshCard` action outcome | Зависит от handler | Handler declares scope (default **CardFull**) |

`drill table from cell` **не** вызывает `ApplyFiltersAsync` и **не** мутирует toolbar field chips.

### 3. Execution / Host contracts

| Layer | Responsibility |
|-------|----------------|
| **Execution** | `QueryCompiler` + optional `CardCellDrillOverlay` только для interior slot compile ([ADR-0064](DASHSPEC-ADR-0064-cell-drill-tabular-payload.md)) |
| **ICardRenderer** | `RenderAsync` = full card; `RenderInteriorSlotsAsync` = secondary slots only |
| **DashboardRefreshCoordinator** | `RefreshSingleCardAsync` → **CardFull**; `RefreshCardInteriorSlotsAsync` → **CardInteriorSlots** |
| **UI** | Primary viz (`CardVisualization`) не размонтируется при **CardInteriorSlots**; обновляется только `CardSlotVisualization` |

### 4. Amendments to prior ADR text

- [ADR-0065](DASHSPEC-ADR-0065-card-interior-multi-slot.md) §3: drill slots при cell drill используют **overlay**, не shared toolbar filters для сужения heatmap.
- [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md): добавить effect `drill table from cell` (whitelist) с scope **CardInteriorSlots**.

## Non-goals (v1)

- Blazor `@key` per slot / streaming partial HTML
- Per-slot `Loading` flag (можно добавить без смены scope table)
- WebSocket push refresh

## Consequences

- Host: реализовать `RefreshCardInteriorSlotsAsync`; cell drill path не вызывает `RefreshSingleCardAsync`.
- Tests: coordinator merge сохраняет `Matrix` на карточке при interior-only refresh.
- Authors: overview LUS — `drill table from cell` на peak / activity.

## SSOT maintenance

При новом click effect или apply path — **сначала** строка в таблице §2, потом код в `DashboardRefreshCoordinator` / controller.
