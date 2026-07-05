# DASHSPEC-ADR-0013: Host SOLID ports and viz plugin registry

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-06-24 |
| **Relates to** | [ADR-0008](DASHSPEC-ADR-0008-viz-render-plugins.md), [ADR-0012](DASHSPEC-ADR-0012-host-presentation-layering.md) |

## Context

После ADR-0012 Host-сервисы оставались concrete-классами; `Home.razor.cs` держал ~340 строк UI-state; `CardVisualization` ветвился по `DataFamily`; поле `render` из diagram preset парсилось, но не влияло на рендер.

## Decision

### Dependency inversion (Host ports)

| Port | Implementation |
|------|----------------|
| `IDashboardSpecLoader` | `DashboardSpecLoader` |
| `ICardRenderer` | `CardRenderService` |
| `IDashboardSession` | `DashboardSessionService` |

`LoadedDashboard` вынесен в `Services/Models/`. Регистрация в `Program.cs` — по интерфейсам.

### Page controller (SRP)

`DashboardPageController` (`Services/Presentation/`) — filter UI state, debounce, tab placement, card refresh. `Home.razor` + `Home.razor.cs` — тонкая оболочка (~20 строк code-behind).

### Viz plugins (O/C, ADR-0008 increment)

- `IVizPlugin` + `VizPluginIds` в `DashSpec.Abstractions`
- `VizPluginRegistry` — resolve `render` из preset или fallback по `DataFamily`
- Built-ins: `chartjs`, `css-grid`, `table-html`, `scalar-html` (in-process, без DLL)
- `CardRenderResult.RenderPluginId` — передаётся в `CardVisualization` (`switch` по plugin id)

Загрузка `[[viz.load]]` из TOML — следующий инкремент (аналог connector plugins).

## Consequences

- Host integration tests могут мокать `IDashboardSession` / `ICardRenderer`.
- Новый backend: реализовать `IVizPlugin`, зарегистрировать в DI (+ позже DLL loader).
- `CardRenderSkeletonFactory` убирает дублирование loading/error placeholders.
