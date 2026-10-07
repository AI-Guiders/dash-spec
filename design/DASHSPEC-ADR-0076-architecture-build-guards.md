# DASHSPEC-ADR-0076: Architecture build guards — analyzers + source rules

| | |
|---|---|
| **Status** | Accepted · partial (layer tests; Platform no-AspNetCore B0 extended)|
| **Date** | 2026-10-02 |
| **Relates to** | [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md), [ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md), [ADR-0085](DASHSPEC-ADR-0085-data-acquisition-architecture-analyzers.md) |

## Context

[ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md) defines Host as a **shell** over a composed view. Regressions (navigation `Count > 1`, date sort/format in UI, payload builders in Blazor) are easy to reintroduce in PRs because nothing **failed the build**.

## Decision

Three enforcement layers (all run on `dotnet build` / CI):

### 1. Roslyn analyzers (`DashSpec.Analyzers`)

Referenced as `Analyzer` from `DashSpec.Host`, `DashSpec.Core`, `DashSpec.Execution.Runtime`, `DashSpec.Execution.Compilation`, and `connectors/*` (see [ADR-0085](DASHSPEC-ADR-0085-data-acquisition-architecture-analyzers.md)).

| Id | Rule |
|----|------|
| **DSCHOST001** | `ChartDataBuilder`, `MatrixPayloadBuilder`, `AxisLabelSort` only under `Services/Rendering/` |
| **DSCHOST002** | `LabelFormat` only under `Services/Rendering/`, `Program.cs`, `Services/Diagnostics/` |
| **DSCHOST003** | `DateValueCodec` forbidden under `Components/` and `Services/Presentation/` |
| **DSPEC020** | SQL client types only under `connectors/**` |
| **DSPEC021** | External I/O forbidden in `Execution.Runtime` |
| **DSPEC030** | Untyped row dictionaries forbidden outside `connectors/**` |
| **DSPEC031–034** | Typed row wire — see [ADR-0087](DASHSPEC-ADR-0087-typed-row-batch-analyzers.md) |

Layer paths: **`LogicalPath`** via `AIGuiders.Platform.Paths` ([ADR-0085](DASHSPEC-ADR-0085-data-acquisition-architecture-analyzers.md)).

Severity: **Error** (blocks compile).

### 2. Source guards (`architecture/host-source-guards.json`) — legacy

Regex rules on `src/DashSpec.Host` paths, validated by `DashSpec.Architecture.Tests`. **New architecture rules use Roslyn (§1) or NetArchTest (§3), not JSON.** This file shrinks as Host shell migration ([ADR-0074](DASHSPEC-ADR-0074-host-shell-composed-view.md)) completes.

- Report nav heuristics (`Tabs.Count > 1`, `Pages.Count > 1`, …) — **ARCH-0074-***.
- Payload APIs in UI/presentation folders.
- Inline Cyrillic in Host UI `.razor` — **ARCH-0077-***; see [ADR-0077](DASHSPEC-ADR-0077-host-ui-copy-ssot.md) and `host-ui-cyrillic-baseline.json`.

**Allowlist** entries are technical debt: each line must link to a composer/view migration task; list must **shrink**, never grow without ADR note.

### 3. Assembly dependency tests (NetArchTest)

`DashSpec.Architecture.Tests`:

- `DashSpec.Host` must not depend on `DashSpec.Modeling.Parse`.
- `DashSpec.Execution.Runtime`, `DashSpec.Viz`, `DashSpec.Filters` must not depend on `DashSpec.Host`.

## Non-goals

- Full ArchUnit for every namespace (add rules incrementally).
- Razor semantic analysis in Roslyn (use source guards for `.razor`).

## Consequences

- New Host “business logic” in wrong folder **breaks CI** immediately.
- Composer P2 (remove `Count > 1`) = delete allowlist rows + fix sources.
