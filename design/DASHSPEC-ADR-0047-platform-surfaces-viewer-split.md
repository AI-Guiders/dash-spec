# DASHSPEC-ADR-0047: DashSpec Platform vs surfaces (Web Host = viewer)

| | |
|---|---|
| **Status** | Proposed |
| **Date** | 2026-09-01 |
| **Relates to** | [ADR-0013](DASHSPEC-ADR-0013-host-solid-ports-viz-registry.md) · [ADR-0015](DASHSPEC-ADR-0015-dev-spec-resolve-dashboard-palette.md) · [ADR-0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md) · [ADR-0041](DASHSPEC-ADR-0041-git-catalog-push-sync.md) · [ADR-0042](DASHSPEC-ADR-0042-host-control-center-witdb.md) · [ADR-0043](DASHSPEC-ADR-0043-filter-command-palette.md) · [GUIDERS-ADR-0048](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0048-authoring-quarry-family.md) · [GUIDERS-ADR-0053](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0053-planet-responsibilities.md) · [GUIDERS-ADR-0055](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0055-surface-wpf-guild-deck-authoring.md) · URSA [ADR 2026-07-07](https://github.com/AI-Guiders/ursa-license-usage/blob/main/docs/adr/ADR_2026-07-07_DashSpec_Product_Boundaries_RU.md) · [dash-spec-studio KB](https://github.com/AI-Guiders/kb/blob/main/knowledge/work/projects/aiguiders-open/dash-spec-studio/README.md) |

## Context

DashSpec today reads as **«Blazor host + DSL»** — one deployable (`DashSpec.Host`) and product `.dashspec` in planet repos (e.g. URSA License Usage). That worked for v0/v1 pilots.

Pressure is building from three directions:

1. **Host overload** — viewer, dev `/dev/spec`, admin/control center, consumption commands, and authoring affordances compete on one web surface ([dash-spec-studio](https://github.com/AI-Guiders/kb/blob/main/knowledge/work/projects/aiguiders-open/dash-spec-studio/README.md) motivation).
2. **Second surface** — **DashSpec Studio** (desktop authoring + Report Preview) needs the **same session/runtime** as Host, not a forked renderer.
3. **Platform thesis** — evolve DashSpec into a **git-native BI platform** (engine + contracts + semantic layer); **Web is one viewer**, not the product definition.

URSA ADR (2026-07-07) already splits **dash-spec platform** vs **planet content** (ETL, warm views, product specs). This ADR names the **horizontal split inside dash-spec**: **Platform** vs **Surfaces**.

> **Note:** [DASHSPEC-ADR-0046](DASHSPEC-ADR-0046-ccl-locale-typed-value-input.md) is reserved for CCL locale adapter. Viewer/Studio/platform split is **0047**.

## Decision

### 1. Product identity

**DashSpec Platform** — headless-capable BI engine and contracts:

- DSL parse / resolve / effective model
- Connectors (plugin families), query budget, parameterized SQL
- Catalog, access, runtime manifest, git catalog push ([0041](DASHSPEC-ADR-0041-git-catalog-push-sync.md))
- Viz/diagram plugin registry ([0013](DASHSPEC-ADR-0013-host-solid-ports-viz-registry.md), [0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md))
- **Report session** — filter state, bind, refresh pipeline, payload builders (shared by all surfaces)
- CommandPlane adapter for consumption commands ([0043](DASHSPEC-ADR-0043-filter-command-palette.md))
- Optional **Presentation** package — shared UI components (extract from Host RCL for Studio parity)

**Surfaces** — deployable apps that **host** a platform session; they do not own DSL semantics:

| Surface | Role | Status |
|---------|------|--------|
| **DashSpec.Host (web)** | **Consumption viewer** — browser deploy for stakeholders; filters, drill, slash palette | **Production** (LUS) |
| **DashSpec Studio (desktop)** | **Authoring viewer** — spec tree, layout board, Data Lab, Script Pad, Report Preview | Planned planet |
| **Embed / SDK** (future) | Partner app, iframe, scheduled render | Latent — second consumer ADR |

**One-liner:** *DashSpec Platform = git-native semantic BI engine. Web Host = deployment viewer. Studio = engineering viewer. One `.dashspec`, many projections.*

### 2. Layer diagram

```text
┌──────────────── DashSpec Platform (dash-spec repo) ────────────────┐
│  Core · Connectors · Catalog · Session · Viz registry            │
│  CommandPlane adapter · Presentation (shared RCL, extract)       │
│  Authoring hooks (dev resolve, validate CLI roadmap)             │
└────────────────────────────┬─────────────────────────────────────┘
                             │ IReportSession / same builders
         ┌───────────────────┼───────────────────┬─────────────────┐
         ▼                   ▼                   ▼                 ▼
   DashSpec.Host        DashSpec Studio      Embed (TBD)      Headless API (TBD)
   Blazor Server        WPF + WebView2       partner          export/schedule
   (viewer)             (authoring viewer)
```

### 3. Host slimming (web viewer responsibilities)

**In scope for Host (consumption):**

- Render resolved report (tabs, cards, filters, viz plugins)
- Consumption command surfaces (toolbar, slash, CCL) — mutate **session** only
- Catalog runtime, API key / access gate
- Git catalog sync webhook consumer ([0041](DASHSPEC-ADR-0041-git-catalog-push-sync.md))
- Ops/admin that **must** be server-side ([0042](DASHSPEC-ADR-0042-host-control-center-witdb.md)) — keep minimal on viewer deploy

**Move out of Host (Studio or platform CLI):**

- Authoring command catalog (`/add card`, `/bind`, layout board editing)
- Data Lab SQL REPL, View-from-REPL promote
- Script Pad / report recipe scaffold
- Dev ergonomics that served as Studio prototype (`/dev/spec` effective tree as **product** UX)

Host may retain **read-only dev endpoints** behind flag until Studio v1 ships; then sunset or proxy to Studio tooling.

### 4. Planet vs platform (federation alignment)

Per [GUIDERS-ADR-0053](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0053-planet-responsibilities.md):

| Owner | Owns |
|-------|------|
| **DashSpec Platform** | DSL grammar, Core, connectors, session contracts, Presentation primitives |
| **Planet** (URSA, IncomeCascade, …) | `.dashspec` content, SQL/ETL, warm views, runtime TOML, domain catalogs |
| **Studio planet** | Desktop chrome, Data Lab, deck (`.deck`), authoring `.catalog` Execute bodies |
| **guiders-platform** | CommandPlane, Authoring quarry (`.catalog`), Cockpit — **not** dashspec grammar |

Planet **never** forks parse/resolve; surfaces **never** embed ETL.

### 5. BI platform roadmap (not v1 scope)

Platform-grade capabilities — incremental, proof by **second surface** (Studio):

| Phase | Capability | Proof |
|-------|------------|-------|
| **v1** | Shared `Presentation` + session between Host and Studio Preview | Studio bootstrap |
| **v1.1** | `dashspec validate` CLI, catalog CI hooks | Planet repos |
| **v2** | Semantic catalog (metric names without view names in every card) | URSA ADR roadmap |
| **v2.1** | Headless render / export API (PDF, PNG, scheduled) | Third surface ADR |
| **v3** | Multi-tenant catalog registry, embed SDK | External consumer |

**Not platform:** full ETL designer, SSMS-class DBA ([dba-studio](https://github.com/AI-Guiders/kb/blob/main/knowledge/work/projects/aiguiders-open/dba-studio/README.md)), proprietary binary report project format.

### 6. Package map (target)

```text
DashSpec.Core                    parse, resolve, bind, query planning
DashSpec.Connectors.*            SqlServer, …
DashSpec.Catalog.*               dashcatalog, git push sync
DashSpec.Presentation            shared Blazor/Hybrid components (extract from Host)
DashSpec.Host                    web viewer shell (thin)
DashSpec.Abstractions            IReportSession, ports for Studio/embed

dash-spec-studio (planet repo)   WPF app, Data Lab, Script Pad, .deck
```

Studio references **platform NuGet** + **Presentation**; does not reference `DashSpec.Host` web project.

### 7. Solo vs team value prop

| Persona | Sees | Platform affordance |
|---------|------|---------------------|
| Stakeholder (Vladimir-style) | **Web viewer** — open URL, filters, tables | None required |
| Engineer / you | Studio + git `.dashspec` | SSOT, diff, CI, agents |
| SSCAD fleet | Agent + API + **same reports in browser** | Deploy once, many viewers later |

Text SSOT pays off when **second editor** or **audit** appears; viewer-first on-ramp stays valid.

## Consequences

- Marketing/engineering can say **«DashSpec BI platform»** without implying «only Blazor site».
- Host repo layout may split: extract `DashSpec.Presentation` before Studio v1.
- URSA prod path unchanged short-term: **DashSpec Host :5295** remains LUS viewer.
- New surfaces require **session parity tests** (same spec → same payload hash in Host and Studio Preview).

## Non-goals (this ADR)

- Implement Studio repo or Presentation extract (follow-up work).
- Merge Host and Studio into one executable.
- Replace guiders-platform federation patterns.
- Commit to embed/mobile viewers before Studio proves shared session.

## Open questions

1. **Presentation package:** Blazor RCL only v1, or shared primitives + Studio-specific WPF chrome ([GUIDERS-ADR-0055](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0055-surface-wpf-guild-deck-authoring.md))?
2. **Headless API:** same process as Host or sidecar worker?
3. **Admin/control center:** stay on Host viewer deploy or move to separate ops surface?
4. **Public 1.0:** does platform/surface split block semver, or only package boundaries?
5. **Rename:** keep product name «DashSpec» for platform + «Host» for web viewer, or «DashSpec Viewer» externally?

## Reference missions

| Planet | Viewer surface | Authoring surface |
|--------|----------------|-------------------|
| URSA LUS | DashSpec Host :5295 (prod) | Studio + `docs/dashspec/` (git) |
| dash-spec dogfood | `DashSpec.Host` localhost | VS Code + `/dev/spec` until Studio |
