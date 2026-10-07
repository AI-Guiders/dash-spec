# DASHSPEC-ADR-0099: Surface.Blazor — full separation and contract testing

| | |
|---|---|
| **Status** | Accepted · partial (B1–B4 + B3 shell DI; L3 bootstrap session test; Host entry still monolithic) |
| **Date** | 2026-10-07 |
| **Relates to** | [ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md), [ADR-0086](DASHSPEC-ADR-0086-surfaces-as-plugins-bff.md), [ADR-0098](DASHSPEC-ADR-0098-compiler-import-namespaces.md), [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) |

## Context

[ADR-0086](DASHSPEC-ADR-0086-surfaces-as-plugins-bff.md) phases **S0–S3** describe surfaces as plugins. Today **`DashSpec.Host`** still owns:

- Report **session** (`DashboardSessionService`, `IDashboardSession`)
- Spec **load/compile** (`DashboardSpecLoader` → `DashSpecParser` / `SpecLibraryComposer`)
- **Render** orchestration (`CardRenderService`)
- Blazor **UI** (`DashSpec.Presentation`, pages, circuit)

Contracts (`IDashboardSession`, `IDashboardSpecLoader`) live under `DashSpec.Host.Services.Abstractions` and reference Host types (`LoadedDashboard`, `SpecLoadOptions`). That blocks:

- Testing **platform behavior** without ASP.NET / Blazor / EF
- Swapping **Surface.Bff** or **Studio** without copying Host internals
- Planet repos (e.g. LUS) depending on a **stable compile + session API**, not on Host layout

[ADR-0098](DASHSPEC-ADR-0098-compiler-import-namespaces.md) adds a **compiler front** (`DashSpecCompiler`, `dashspec.toml` index). That belongs to **Platform**, not to a Blazor deployable.

**Decision:** **fully separate** Platform session/compile from **Surface.Blazor**; validate Platform through **contract tests** that do not reference any surface assembly.

## Decision

### 1. Three layers (normative)

```text
┌─ DashSpec.Platform.* ─────────────────────────────────────────────┐
│  Modeling.Parse (F#) — DashSpecCompiler, import index, diagnostics │
│  Execution.Compilation — library compose, resolve                    │
│  Execution.Runtime — ReportSession (impl), spec bootstrap, refresh │
│  Abstractions — IReportSession, IReportCompiler, wire DTOs (evolve) │
└────────────────────────────┬──────────────────────────────────────┘
                             │ in-proc only; no ASP.NET
┌─ DashSpec.Surface.Blazor ─┴───────────────────────────────────────┐
│  ASP.NET host, SignalR circuit, DI glue, catalog/git ops (viewer)   │
│  References: Platform + Presentation RCL + Viz plugins            │
└────────────────────────────┬──────────────────────────────────────┘
                             │ optional later
┌─ DashSpec.Surface.Bff ───┴──────────────────────────────────────┐
│  HTTP/OpenAPI over same IReportSession (ADR-0086 S2)                │
└─────────────────────────────────────────────────────────────────────┘
```

**Rule:** `DashSpec.Platform.*` projects **must not** reference `Microsoft.AspNetCore.*`, `DashSpec.Presentation`, or `DashSpec.Host`.

**Rule:** `DashSpec.Surface.Blazor` **must not** call `DashSpecParser.Parse` directly; it uses **`IReportCompiler`** / session load APIs from Platform.

### 2. Contract surface (Abstractions)

Move and rename (incremental; old names deprecated):

| Today (Host) | Target (Abstractions) |
|--------------|------------------------|
| `IDashboardSession` | `IReportSession` |
| `IDashboardSpecLoader` | `IReportBootstrap` or method on `IReportCompiler` |
| `LoadedDashboard` | `ReportBootstrapResult` (no Host namespace) |
| `CardRenderResult` | move to Abstractions.Session or Abstractions.Render |

Session responsibilities **unchanged** semantically: load catalog/spec, filter state, connector resolution, card render **payload** production (not Blazor markup).

**Compile** responsibilities:

| API | Owner |
|-----|--------|
| `DashSpecCompiler.compile` (F#) | Modeling.Parse — C# adapter `IReportCompiler` in Execution.Compilation |
| `dashspec.toml` project index | Modeling.Parse.Project — consumed by compile only |

### 3. Package map (target sln)

| Package | Role |
|---------|------|
| `DashSpec.Modeling.Parse` | F# compiler pipeline (0098) |
| `DashSpec.Execution.Compilation` | C# bridge, `SpecLibraryComposer`, `IReportCompiler` |
| `DashSpec.Execution.Runtime` | `ReportSession` implementation (extract from Host) |
| `DashSpec.Abstractions` | `IReportSession`, bootstrap/render ports, payload DTOs |
| `DashSpec.Surface.Blazor` | **New** — current `DashSpec.Host` viewer + `Program.cs` |
| `DashSpec.Host` | **Alias deploy** — thin meta-package or renamed entry (transition: Host → Surface.Blazor) |
| `DashSpec.Presentation` | Blazor RCL only; referenced by Surface.Blazor |

### 4. Contract testing (why full separation)

Test layers **do not** require Blazor, browser, or planet SQL:

| Layer | Project (direction) | Proves |
|-------|---------------------|--------|
| **L1 — Language** | `DashSpec.Modeling.Parse.Tests` | Parse, import header, index, `compileReportModule` |
| **L2 — Compile** | `DashSpec.Execution.Compilation.Tests` | `IReportCompiler` on `samples/demo`, golden document id / wiring graph |
| **L3 — Session** | `DashSpec.Platform.Session.Tests` (new) | `IReportSession` with **fake connector** + in-memory spec text; filter mutate; render payload hash |
| **L4 — Surface** | `DashSpec.Surface.Blazor.Tests` / E2E | DI wiring, one smoke page (optional) |

**L3 is the independence goal:** Platform session tested against **fakes** implementing `IDataSourceConnector`; no `WebApplicationFactory` unless testing Surface.

Contract tests may use:

- `tests/Fixtures/import-project/` (Demo.*, ADR-0098)
- `samples/demo/` (legacy `!include` until demo migrates separately)

Planet specs (**Lus.*** in URSA) are **out of dash-spec** CI; planets run their own contract suite against published Platform packages.

### 5. Phased delivery (this ADR)

| Phase | Deliverable | Proof |
|-------|-------------|-------|
| **B0** | This ADR + analyzer rule: Platform → no AspNetCore | Build graph |
| **B1** | `IReportSession` + types in `Abstractions`; Host implements via type forward / adapter | Host tests green |
| **B2** | `ReportSession` + `ReportSpecBootstrap` in `Execution.Runtime`; Host deletes duplicate logic | L3 session tests |
| **B3** | `DashSpec.Surface.Blazor` project; Host references it or rename | Same E2E |
| **B4** | `IReportCompiler` wraps F# `DashSpecCompiler`; loader uses index | L2 compile tests |
| **B5** | Planet LUS: `dashspec.toml` + `import` (URSA repo only) | LUS soak parse in planet CI |

**Order vs 0098:** B4 can overlap B2; **planet migration (B5) after B2** so LUS does not depend on Host internals.

### 6. What stays on Surface.Blazor only

- Catalog webhook, WitDB control center ([0042](DASHSPEC-ADR-0042-host-control-center-witdb.md))
- Command plane slash UI ([0043](DASHSPEC-ADR-0043-filter-command-palette.md))
- File watchers for dev reload (may call `IReportCompiler` reload hook)
- Windows service hosting, Kestrel, static files

### 7. Non-goals (B0–B3)

- Surface.Bff HTTP (0086 S2)
- Studio desktop
- Mandating Blazor for partners
- Moving viz plugins out of Surface deploy (still bundled with Blazor reference surface)

## Consequences

- **Product language:** «Platform + Surface.Blazor deploy»; compile/import docs point to Platform API.
- **LUS / planets:** integrate via **NuGet/platform refs** + own `dashspec.toml`; no SSCAD-specific examples in `dash-spec` samples (Demo.* only).
- **Regression safety:** L3 contract suite runs on every PR without browser.

## Summary

- **Fully separate** Platform (compile + session) from **Surface.Blazor** (ASP.NET viewer).
- **Test Platform by contracts** (L1–L3) without Host or Blazor.
- **LUS `import` migration** follows Platform extraction (B5), not the other way around.
