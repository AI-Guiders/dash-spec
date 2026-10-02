# DASHSPEC-ADR-0085: Data Acquisition vs Data Flow — boundaries and DSPEC analyzers

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-02 |
| **Relates to** | [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md), [ADR-0083](DASHSPEC-ADR-0083-dataflow-engine-cockpit-transport.md), [ADR-0076](DASHSPEC-ADR-0076-architecture-build-guards.md), CIDE [ADR-0102](https://github.com/AI-Guiders/cascade-ide/blob/main/docs/adr/0102-data-acquisition-layer-boundary-and-contract.md) |

## Context

Runtime today still materializes card data as `IReadOnlyDictionary<string, object?>` ([0078](DASHSPEC-ADR-0078-dashflow-data-plane.md) targets typed `rows R`). SQL I/O must stay in connector plugins ([ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md)). Cockpit/CIDE already enforces **DAL vs CCU** via analyzers (CDPCOPE020) and **LogicalPath** (GUIDERS-ADR-0050).

## Decision

### Layers (normative)

| Layer | Repo locus (logical path) | Role |
|-------|---------------------------|------|
| **Data Acquisition** | `connectors/**` | `IDataSourceConnector`, SqlClient/Npgsql, timeouts, `max_rows` |
| **Data Flow** (future) | `src/DashSpec.Execution.*` transforms, Modeling `FlowGraph` | Typed ports, no new SQL connections |
| **Present** | Host rendering, viz payloads | Consume batches; no SQL |

### Path rules (platform-aligned, no JSON regex)

Layer prefixes are **repo-relative logical paths** (`connectors/`, `src/DashSpec.Execution.Runtime/`, …) matched with the same normalization rules as **`LogicalPath`** (GUIDERS-ADR-0050).

`DashSpec.Analyzers` ships **`LogicalPathCompat`** (netstandard2.0, RS1041/RS1035 — no reference to `AIGuiders.Platform.Modeling.Paths` yet). Runtime tools and tests should use **`PathBoundary.ToLogical`** from the Paths package where TFM allows.

No regex path guards for acquisition rules.

### Roslyn (`DashSpec.Analyzers`)

Referenced as `Analyzer` from Host, Core, Execution.Runtime, Execution.Compilation, connector projects.

| Id | Rule |
|----|------|
| **DSPEC020** | SQL client types (`SqlConnection`, `NpgsqlConnection`, …) forbidden outside `connectors/**` |
| **DSPEC021** | `File` / `Directory` / `Process` / `HttpClient` forbidden under `src/DashSpec.Execution.Runtime/**` |
| **DSPEC030** | `Dictionary<string, object?>` / `IReadOnlyDictionary<string, object?>` forbidden outside `connectors/**` (use `RowBatch` / `DataRow`) |
| **DSCHOST001–003** | Unchanged ([ADR-0076](DASHSPEC-ADR-0076-architecture-build-guards.md)) |

Severity: **Error**.

### Assembly guards

`DashSpec.Architecture.Tests`: `Execution.Runtime` must not depend on `Microsoft.Data.SqlClient`.

### `host-source-guards.json`

Legacy Host/Razor heuristics ([0076](DASHSPEC-ADR-0076-architecture-build-guards.md) §2). **Do not extend** for acquisition; new rules → Roslyn DSPEC* or NetArchTest.

## Phased delivery

| Phase | Deliverable |
|-------|-------------|
| **A0** | DSPEC020/021 + analyzer refs + NetArchTest SqlClient |
| **A1** (shipped) | `RowBatch` / `DataRow` on `IDataSourceConnector`; DSPEC030 **Error** (no strangler) |
| **A2** | F# `FlowGraphValidation` + NetArchTest Modeling.* |

## Non-goals

- Replacing IDE Health / Cockpit analyzers in dash-spec repo.
- FSharp.Analyzers in A0 (Modeling uses validation tests first).
