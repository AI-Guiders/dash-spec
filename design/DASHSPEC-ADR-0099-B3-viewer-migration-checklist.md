# ADR-0099 B3 — viewer migration checklist (Host → Surface.Blazor)

| | |
|---|---|
| **Status** | Accepted · in progress |
| **Relates to** | [ADR-0099](DASHSPEC-ADR-0099-surface-blazor-contract-testing.md) B3 |

## Current state (`develop` after B3.3 WIP)

- `DashSpec.Surface.Blazor` — **viewer surface** (dashboard `.razor`, `Commands/*`, session/presentation/rendering, `DashCatalog` partial + emit link).
- **Host shell** — `App.razor`, `Routes.razor`, `Admin.razor`; planet bootstrap, adapters, endpoints.
- **UI RCL** — `DashSpec.Presentation` (filter chrome); viewer UI root is Surface, not Host.
- **Viewer plugins** — `DashSpec.Viewer` (`Plugins/*`, `IViewerPluginHost`); Host loaders + diagnostics/on-click host adapters.
- **Session / render / command palette** — `DashSpec.Host` (`DashboardSessionService`, `CardRenderService`, `Commands/*`, `Services/Presentation/*`).
- **Planet** — bootstrap, `DashSpecHostContext`, connectors, git/catalog, EF, endpoints — `DashSpec.Host`.

Goal **B3 canon**: viewer surface testable and packaged without treating Host as the UI root.

## Blocker discovered (2026-10-07)

A naive `git mv` of Components + Commands + Presentation into Surface breaks **`Surface → must not → Host`** because viewer code directly references:

- `DashSpec.Host.Plugins` / `Builtins`
- `DashSpec.Host.Services.Dev`, `Diagnostics`, `Settings`, `Git`
- `DashSpecHostContext`, `DashSpecParseOptionsProvider`

**Order matters:** extract plugin/catalog **ports** before moving `.razor` trees.

## Checklist (all required for B3 «без partial»)

| Step | Deliverable | Proof |
|------|-------------|--------|
| **B3.1** | `ICardRenderer`, `IDashboardSession` in `DashSpec.Viz.Platform` (extend `IReportSession`); loader via `IReportSpecBootstrap` / `ReportBootstrapResult`; `CatalogBootstrap` / `CatalogSourceState` in `DashSpec.Core.Catalog`; `IHostPathResolver`, `IViewerRuntimeContext` in `DashSpec.Abstractions.Hosting` | **Done** (`develop`): Host aliases + `ViewerRuntimeContextAdapter`; build + Host.Tests + Arch + L3 |
| **B3.2** | `DashSpec.Viewer` + `DashSpec.Viewer.Plugins` (registries, bootstrap, builtins); `IViewerPluginHost` in `Abstractions.Viewer`; Host keeps loaders + planet builtins (`Diagnostics`, `OnClickInteractionService`) | **Done** (`develop`): Arch `Viewer_must_not_reference_Host` + `Surface_Blazor_must_not_reference_Host`; Surface refs Viewer |
| **B3.3** | `DashSpec.Surface.Blazor` → `Sdk.Razor`; move `Components/`, `Commands/`, `Services/Presentation`, `Services/Rendering`, `DashboardSessionService` | **WIP** (`develop` local): build + Host.Tests 111 + Arch 13 + L3 3; Host keeps `App`/`Routes`/`Admin` |
| **B3.4** | Host `Program`: `AddDashSpecBlazorViewerShell` + `AddDashSpecBlazorViewerSession` + `AddDashSpecHostViewerPlatform` (adapters only) + deploy services | `Program` has no viewer logic blocks |
| **B3.5** | Arch: `Host_must_not_reference_Presentation`; existing `Surface_Blazor_must_not_reference_Host` | `DashSpec.Architecture.Tests` green |
| **B3.6** | Smoke: `DashSpec.Host.E2E` or one `WebApplicationFactory` open `/` | CI green |

## Non-goals

- URSA / planet specs (forge soak only).
- `DashSpec.Surface.Bff` (ADR-0086 S2).

## Definition of done (goal close)

All checklist rows **B3.1–B3.6** green; ADR-0099 header B3 line updated to **Implemented** (not partial).
