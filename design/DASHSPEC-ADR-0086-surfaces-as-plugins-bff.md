# DASHSPEC-ADR-0086: Surfaces as plugins — viewer-agnostic platform + BFF

| | |
|---|---|
| **Status** | Accepted · partial (S0–S1 in repo; BFF S2 not started)|
| **Date** | 2026-10-02 |
| **Relates to** | [ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md), [ADR-0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md), [ADR-0013](DASHSPEC-ADR-0013-host-solid-ports-viz-registry.md), [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0083](DASHSPEC-ADR-0083-dataflow-engine-cockpit-transport.md), [ADR-0085](DASHSPEC-ADR-0085-data-acquisition-architecture-analyzers.md) |

## Context

[ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) names **Platform vs Surfaces** and lists Web Host, Studio, embed/headless as future consumers. In practice **DashSpec.Host (Blazor Server)** still reads as *the* product deploy.

Product pressure:

- Stakeholders need a **browser viewer**; engineers may need **desktop Studio**; partners may ship **their own UI** (Angular, embedded analytics shell, vendor stacks comparable to agent-driven BI demos).
- From a **platform** perspective, *who renders pixels* is irrelevant: the contract is **resolved report + session + data payloads**, not Razor or SignalR.
- Connectors are already **plugins** ([ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md)); surfaces should follow the same **microkernel** rule ([ADR-0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md)): platform owns semantics; deployables plug in.

## Decision

### 1. Normative split (unchanged from 0047, sharpened)

| Layer | Owns | Does **not** own |
|-------|------|------------------|
| **DashSpec Platform** | Parse/resolve, connectors, catalog, **report session**, query compile, DataFlow execution ([0078](DASHSPEC-ADR-0078-dashflow-data-plane.md)), payload construction (until typed ports land), filter bind, access policy | UI framework, routing chrome, partner branding |
| **Surface plugin** | Transport + presentation: HTTP BFF, Blazor circuit, WPF window, embed SDK | DSL grammar, SQL acquisition, ETL |

**Viewer is not part of platform identity.** Blazor Host is the **reference surface** for demo and dogfood, not a hard dependency.

### 2. Surface plugin family

Treat **Surface** as a **plugin family** alongside Connector and Viz ([ADR-0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md)):

```text
DashSpec.Platform (Execution + Modeling + Abstractions)
        │
        ├── Connectors.*     IDataSourceConnector  (SQL, …)
        ├── Viz.*            IVizPlugin            (chart/table/matrix render)
        └── Surfaces.*       ISurfaceHost          (session lifecycle + wire API)
```

| Surface (examples) | Deploy shape | Consumer |
|--------------------|--------------|----------|
| **Surface.Blazor** | `DashSpec.Surface.Blazor` + `DashSpec.Host` deploy entry | Browser, SSR/circuit |
| **Surface.Bff** | ASP.NET (minimal API) or reverse-proxy + same session in-process | SPA (Angular, React, Vue), mobile shell |
| **Surface.Studio** | Desktop host (WPF/WebView2 per [0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md)) | Authoring + preview |
| **Surface.Partner** | Partner-owned BFF calling platform packages | QSense-like shells, Diasoft-style portals, iframe embed |

New surfaces **do not fork** parse/resolve or connectors; they **host** the same `IReportSession` (or successor) and call the same render pipeline as `CardRenderService` today.

### 3. BFF contract (wire, not Blazor)

A **BFF surface** exposes a **stable HTTP (or gRPC) API** over the in-process platform session:

| Concern | Platform (in-proc) | BFF exposes (v1 direction) |
|---------|----------------------|----------------------------|
| Bootstrap | Load spec + runtime manifest | `GET /dashboards/{id}` — metadata, tabs, filter defs |
| Filter state | Session mutation | `PATCH /session/filters` |
| Refresh | Connector + builders | `POST /session/refresh` → card payloads |
| Card body | `ChartPayload` / `TablePayload` / `MatrixPayload` / scalar DTO | JSON schema documented; version field |
| Drill / interaction | Session + filter wire ([0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md)) | `POST /cards/{id}/interactions` |
| Auth / catalog | Host gates today | Same policy hooks; BFF does not bypass acquisition |

Rules:

- **BFF is thin:** no `SqlConnection`, no duplicate `QueryCompiler` ([ADR-0085](DASHSPEC-ADR-0085-data-acquisition-architecture-analyzers.md)).
- **DTOs are surface-facing;** internal `Dictionary<string, object?>` rows are **not** the long-term wire format ([0079](DASHSPEC-ADR-0079-dashflow-type-system.md) typed batches later).
- **OpenAPI** (or equivalent) is the integration doc for third-party frontends; `.dashspec` remains git SSOT for report definition.

Blazor Host may **also** call the same abstractions internally (no requirement for Host to HTTP-call itself in v1); extracting a shared **session façade** is incremental.

### 4. Viz plugins vs surface plugins

| Plugin | Boundary |
|--------|----------|
| **Viz** ([0013](DASHSPEC-ADR-0013-host-solid-ports-viz-registry.md)) | Maps **payload DTO** → UI component (Blazor today) |
| **Surface** | Maps **HTTP/UI shell** → **session** + dispatches refresh |

A SPA surface ships **its own viz** (e.g. ECharts, vendor widgets) as long as it consumes **platform payload JSON**. Optional: reuse viz semantics via documented payload schema, not via Blazor RCL.

### 5. Packaging target (names indicative)

```text
DashSpec.Abstractions          IReportSession, ISurfaceHost, wire DTOs (evolve)
DashSpec.Execution.*         session, bind, render pipeline (framework-agnostic)
DashSpec.Surface.Blazor      viewer RCL + session UI (Host references Surface + Presentation)
DashSpec.Surface.Bff         ASP.NET host template + OpenAPI (future package)
DashSpec.Connectors.*        unchanged
```

Planets (demo, …) deploy **a surface + connectors + runtime TOML**, not «fork Host».

### 6. Federation alignment

- **DataFlow** ([0083](DASHSPEC-ADR-0083-dataflow-engine-cockpit-transport.md)) stays **below** surfaces: any viewer subscribes to the same executor/snapshots.
- **Cockpit** (IDE Health, attention) is **optional**; BI BFF does not require Cockpit.
- **guiders-platform** CommandPlane / consumption commands ([0043](DASHSPEC-ADR-0043-filter-command-palette.md)) may be exposed on BFF as REST where slash/CCL is not available.

## Phased delivery

| Phase | Deliverable | Proof |
|-------|-------------|-------|
| **S0** (now) | ADR + package boundaries in docs; analyzers on Execution not «Host = platform» ([0085](DASHSPEC-ADR-0085-data-acquisition-architecture-analyzers.md)) | demo Blazor deploy unchanged |
| **S1** | Extract **session façade** from `DashSpec.Host` into `Abstractions` + tests; Host = thin | Same payload hash before/after |
| **S2** | **Surface.Bff** minimal API: filters + refresh + one dashboard | Angular or curl client |
| **S3** | OpenAPI + auth parity; optional **Surface.Partner** sample | External team integrates without Blazor |

## Consequences

- Product language: **«DashSpec platform + surface deploy»** — e.g. «demo on Surface.Blazor», «partner on Surface.Bff».
- New frontends are **integration projects**, not forks of `DashSpec.Core`.
- Host slimming ([0074](DASHSPEC-ADR-0074-host-shell-composed-view.md), [0076](DASHSPEC-ADR-0076-architecture-build-guards.md)) applies to **Blazor surface only**, not to platform core.
- Second-surface parity tests from [0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) extend to **BFF JSON** vs Blazor render.

## Non-goals

- Implement Angular or partner UI in dash-spec repo.
- Replace Blazor Host as default demo deploy in S0–S1.
- Mandate a single UI framework for all planets.
- Expose raw SQL or connector strings on BFF (acquisition stays server-side).

## Open questions

1. **Session affinity:** Blazor circuit vs stateless BFF + server-side session store — one ADR when S2 starts.
2. **Presentation RCL:** shared only for Blazor/Studio hybrid, or split DTO schema from any UI package?
3. **Semver:** is `Surface.Bff` API a separate versioned package from `DashSpec.Core`?
