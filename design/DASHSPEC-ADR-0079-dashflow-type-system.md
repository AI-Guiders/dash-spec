# DASHSPEC-ADR-0079: Dashflow type system — strict static, value-only

| | |
|---|---|
| **Status** | Accepted (normative for dashflow; phased implementation) |
| **Date** | 2026-10-02 |
| **Relates to** | [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0006](DASHSPEC-ADR-0006-sql-datasource-and-sqldialect.md), [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) |

## Context

Today, query results flow as **`Dictionary<string, object?>`** and diagram bindings are **untyped column names**. Runtime coercions (`LabelFormat`, `DateValueCodec`, axis sort hacks) compensate at execution time. That blocks:

- compile-time wiring checks on the data flow graph;
- schema preview per node (Data Flow Designer);
- safe refactor when views or columns change.

The dashflow model ([ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md)) **requires** a type on every port. This ADR defines that system.

## Decision

### 1. Strict static typing (MUST)

| Rule | Meaning |
|------|---------|
| **No dynamic typing** | No `object`, `json`, or “any column” in flow IR. Untyped bags are **connector-internal only** until the channel boundary. |
| **Compile-time graph check** | Every edge `(outPort → inPort)` must be provably compatible in **Modeling** (F#). Mismatch = error diagnostic with span. |
| **Declared or locked inference** | Every channel/transform output has an explicit `DashType`. SQL inference may **propose** a type; shipping spec must **declare or lock** it (see §5). |

Runtime may still use CLR values; **authoring and graph IR do not**.

### 2. Value types only (no reference types)

The type system describes **values**, not object identity.

| Allowed | Not in v1 type system |
|---------|------------------------|
| Primitives (§3) | Reference types, `class`, entity identity |
| `record` (product of fields) | Pointer / shared mutable row handles |
| `optional T` | `null` as a silent third state without `optional` |
| `rows R` where `R` is a record | Opaque cursor types on ports |

`rows R` is a **value snapshot stream** at the type level (immutable batch semantics). Execution may stream implementation-wise; the **contract** is value-shaped.

### 3. Canonical DashSpec types (SSOT — not SQL, not .NET)

**Do not** make T-SQL or C# type names the authoring SSOT. Dialects disagree (`DATETIME2` vs `timestamp`, `NVARCHAR` length, timezone). Execution targets .NET; storage targets SQL — both **map from** DashSpec.

#### Primitives (closed set, grow only by ADR)

| DashSpec | Meaning |
|----------|---------|
| `bool` | |
| `int` | 32-bit signed integer |
| `decimal` | Fixed-scale decimal (scale in predicate or mapping metadata) |
| `string` | Unicode text (length/refinement via predicate) |
| `instant` | Absolute UTC point in time (wire: ISO-8601 UTC) |
| `local_date` | Calendar date without timezone (reporting calendar) |
| `local_datetime` | Local wall-clock without storing offset in the value |
| `duration` | Elapsed time (not a clock instant) |

**Forbidden as data-plane types:** `string` masquerading as `instant` / `local_date` on ports (display formatting belongs in **present**, or derived typed columns from `semantic_time`).

#### Aggregates

| Form | Use |
|------|-----|
| `record { f: T, … }` | One row shape |
| `rows R` | Port type for tables / card inputs (`R` = record) |
| `list T` | Rare; scalars series — prefer `rows` with one column |

#### User-defined types (UDT)

```text
type StakeholderKpiRow = record {
  usage_day: local_date
  user_sam: string
  peak_concurrent_apps: int
}
```

- UDT names are **nominal** (reuse across channels, transforms, cards).
- UDT bodies are **structural records** (value types only).
- Optional file root: `.dashtype` / `types { }` block in module ([ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md) extended when parser lands).

#### Filter value types (separate from row types)

Dashboard / report graph filter ports use dedicated types, e.g.:

| Type | Role |
|------|------|
| `date_range` | Closed-open range on `local_date` or `instant` (step declares which) |
| `field_set<string>` | `IN` selection |
| `top_n` | Limit semantics |

Filter **application** lives in the **report internal flow** ([ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md) amendment), not inside channel nodes.

### 4. Predicates (refinements on value types)

Not a programming language — **constraints** on fields or type aliases:

```text
type UserSam = string where non_empty
type PeakApps = int where >= 0
type UsageDay = local_date
```

| Rule | Meaning |
|------|---------|
| Predicates are **compile-time** checks where provable | e.g. non-empty literal, known schema |
| Predicates may be **runtime-validated** at channel boundary | e.g. SQL row violates `>= 0` → diagnostic at fetch |
| No arbitrary expressions | Closed catalog: `non_empty`, `>= 0`, `precision(p,s)`, `max_length(n)`, … |

Predicates enable richer Designer hints without turning DSL into a YP.

### 5. SQL and .NET — mapping layers, not the language

```text
SQL column metadata  ──infer──►  DashSpec type  ──emit──►  .NET storage
                                      ▲
                                 author UDT / lock
```

#### SQL → DashSpec (inference, per `@sqldialect`)

| T-SQL (illustrative) | DashSpec |
|----------------------|----------|
| `bit` | `bool` |
| `int`, `bigint` | `int` (widen rules explicit) |
| `decimal(p,s)` | `decimal` + predicate `precision(p,s)` |
| `nvarchar(n)` / `varchar(n)` | `string` + `max_length(n)` |
| `date` | `local_date` |
| `datetime2`, `datetimeoffset` (UTC normalized) | `instant` |
| `time` | `duration` or `time_of_day` (ADR add if needed) |

Ambiguous columns (untyped `sql` ad-hoc) **must** be annotated in channel output before graph wiring.

#### DashSpec → .NET (Execution.Runtime)

| DashSpec | .NET (normative wire) |
|----------|------------------------|
| `bool` | `bool` |
| `int` | `int` |
| `decimal` | `decimal` |
| `string` | `string` |
| `instant` | `DateTime` (UTC) or `DateTimeOffset` (UTC) — pick one in Execution ADR note; Modeling uses `instant` only |
| `local_date` | `DateOnly` |
| `local_datetime` | `DateTime` (unspecified kind) + reporting zone in transform metadata |
| `optional T` | `T?` |

Authors **never** write `DateTime` in `.dashspec` / `.dashflow`.

### 6. Ports and transforms (typed signatures)

Every node port:

```text
output kpi: rows StakeholderKpiRow
input raw: rows StakeholderKpiRow
```

Transform steps declare **type transformers** in the catalog, e.g.:

```text
semantic_time: (rows R, zone, …) → rows R'
```

where `R'` is computed by Modeling rules (fields added/changed/kind-upgraded `instant` → `local_date`). Invalid step chain = compile error.

**Card `input`** must match `rows R`; **diagram** bindings reference fields of `R` with kind checks (`x` expects temporal kinds for time axis, etc.).

### 7. Channel param surface vs filter values

Channel may declare **param surface** (names + filter **types** compatible with dashboard filters). **Values** are wired only in **report internal flow** (`apply_filters` node). Channel `out` row type is **not** parameterized by filter state in the type system — filtered output is a **downstream port** with the same or narrowed `rows R` type.

## Phased delivery

| Phase | Deliverable |
|-------|-------------|
| **T0** | F# `DashType` DU + predicates; port typing on `FlowGraph` |
| **T1** | SQL schema infer → proposed types; lock file / explicit `output` |
| **T2** | Diagram field compatibility vs `rows R` |
| **T3** | Designer schema preview from Modeling |

## Non-goals

- Reference / entity types, ORM identity, graph DB pointers.
- Dependent types (length of list = f(n)).
- User-defined functions or arbitrary expressions in types.
- Replacing SQL or .NET — only **mapping** them.

## Consequences

- Data Flow Graph / Designer becomes feasible: ports are typed; n8n-style wiring is **validation**, not decoration.
- `object?` row bags shrink to connector adapter boundary; payload builders consume **typed rows**.
- LUS / view drift detected when locked UDT ≠ inferred SQL schema.
