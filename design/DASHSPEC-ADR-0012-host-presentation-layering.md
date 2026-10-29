---
type: ADR
id: DASHSPEC-ADR-0012
title: "Host presentation layering"
status: "Accepted"
date: 2026-06-24
tags: [dashspec, adr]
language: ru
relates:
  - DASHSPEC-ADR-0011
---

# DASHSPEC-ADR-0012: Host presentation layering

## Context

После рефакторинга Core (`Parsing` / `Analysis` / `Resolution`) Host оставался монолитом: `Home.razor` ~700 строк, `DashboardSessionService` смешивал load, query и render.

## Decision

### Host services

| Слой | Каталог | Ответственность |
|------|---------|-----------------|
| **Loading** | `Services/Loading/` | parse spec, resolve, field options |
| **Rendering** | `Services/Rendering/` | `CardRenderService` — connector query → payloads |
| **Session** | `Services/DashboardSessionService.cs` | runtime state, filter mutations, orchestration |
| **Presentation** | `Services/Presentation/` | UI helpers (layout, filter labels) |

### Blazor UI

| Компонент | Роль |
|-----------|------|
| `DashboardToolbar` | upload spec |
| `DashboardTabBar` | вкладки |
| `DashboardFiltersSection` | toolbar filters |
| `DashboardCardView` | карточка + local filters |
| `CardVisualization` | chart / table / scalar / heatmap body |
| `Home.razor` + `Home.razor.cs` | page shell (~20 lines code-behind) |
| `DashboardPageController` | filter UI state, refresh orchestration ([ADR-0013](DASHSPEC-ADR-0013-host-solid-ports-viz-registry.md)) |

### Core payloads

`ChartDataBuilder` — facade; builders по семейству: `ChartSeriesPayloadBuilder`, `TablePayloadBuilder`, `MatrixPayloadBuilder`, `PayloadRowFormatters`.

## Consequences

- `CardRenderResult` / `DashboardHostOptions` в `Services/Models/`.
- Новые viz kinds: builder в Core `Runtime/`, renderer в `Components/Dashboard/`.
