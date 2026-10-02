# DASHSPEC-ADR-0087: Typed row batch (`rows R`) — wire model and DSPEC031–034

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-02 |
| **Relates to** | [ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md), [ADR-0085](DASHSPEC-ADR-0085-data-acquisition-architecture-analyzers.md), [ADR-0076](DASHSPEC-ADR-0076-architecture-build-guards.md) |

## Context

[ADR-0085](DASHSPEC-ADR-0085-data-acquisition-architecture-analyzers.md) **A1** removed `Dictionary<string, object?>` from the data plane (DSPEC030) but left **CLR `object?` cells** in `RowBatch` / `DataRow`. That is **not** the [ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md) contract: ports carry **`rows R`** with a **declared row aggregate** and **DashSpec primitive** cells at the wire boundary.

## Decision

### 1. Normative wire (Execution, post-acquisition)

| Type | Role |
|------|------|
| **`RowTypeSchema`** | Nominal row type name + ordered `RowFieldSchema` (field name + `DashPrimitiveKind` + optional) |
| **`DashValue`** | Closed-set primitive cell (no `object` on the wire) |
| **`TypedRowBatch`** | `Schema` + `IReadOnlyList<TypedDataRow>` |
| **`TypedDataRow`** | Ordinal-backed getters (`GetInt32`, `GetString`, `GetDateTimeUtc`, …) |

SQL-shaped **inferred flat rows** use generated type names (e.g. `SqlInferredRow`) until author UDTs land in Modeling; schema is still **required** on every batch.

**Forbidden in data plane:** `RowBatch`, `DataRow`, `object?[]` row buffers, `GetValueOrDefault(string)` on rows.

**Allowed only in acquisition materialization** (`connectors/**`, `Abstractions/Data/Acquisition/**`): read `DbDataReader`, map to `DashValue`, construct `TypedRowBatch`.

### 2. Roslyn (extends [0085](DASHSPEC-ADR-0085-data-acquisition-architecture-analyzers.md))

| Id | Layer | Rule |
|----|-------|------|
| **DSPEC030** | data plane + Abstractions (except Acquisition) | No `Dictionary<string, object?>` row bags |
| **DSPEC031** | data plane | No `object?[]` (untyped cell buffers) |
| **DSPEC032** | data plane + Abstractions (except Acquisition) | No `RowBatch.Create` / legacy materialization API |
| **DSPEC033** | data plane | No `DataRow.GetValueOrDefault` / `TryGetValue` |
| **DSPEC034** | data plane | No type references to `RowBatch` or `DataRow` |

Severity: **Error**. Tests (`tests/**`) are out of scope for DSPEC031–034 (fixtures may build via `TypedRowBatch` APIs only).

### 3. Connector contract

`IDataSourceConnector.QueryAsync` returns **`TypedRowBatch`** with schema inferred from the reader ([0079](DASHSPEC-ADR-0079-dashflow-type-system.md) §5 SQL→DashSpec mapping).

### 4. Phasing

| Phase | Deliverable |
|-------|-------------|
| **B0** | This ADR + DSPEC031–034 + `TypedRowBatch` model |
| **B1** | Migrate Execution / Core / Host; remove `RowBatch` |
| **B2** | Author `type` … `end type` → `RowTypeSchema` from Modeling (replaces inference names) |

## Non-goals (B0)

- Nested UDT cells (Address inside OrderDetail) — flat SQL rows only; nested wire in a later ADR.
- `duration` / `Week` / `Month` stdlib instances on wire — kinds exist; mapping rules grow with transforms.
