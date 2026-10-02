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
| UDT (`type` … `end type`) | Pointer / shared mutable row handles |
| `optional` on a field line | `null` as a silent third state without `optional` |
| `rows R` where `R` is a UDT | Opaque cursor types on ports |

`rows R` is a **value snapshot stream** at the type level (immutable batch semantics). Execution may stream implementation-wise; the **contract** is value-shaped.

### 3. Canonical DashSpec types (SSOT — not SQL, not .NET)

**Do not** make T-SQL or C# type names the authoring SSOT. Dialects disagree (`DATETIME2` vs `timestamp`, `NVARCHAR` length, timezone). Execution targets .NET; storage targets SQL — both **map from** DashSpec.

#### Primitives (closed set, grow only by ADR)

| DashSpec | Meaning |
|----------|---------|
| `bool` | |
| `int` | 32-bit signed integer |
| `decimal` | Fixed-scale decimal (precision/scale in SQL mapping metadata when inferred) |
| `string` | Unicode text |
| `instant` | Absolute UTC point in time (wire: ISO-8601 UTC) |
| `local_date` | Calendar date without timezone (reporting calendar) |
| `local_datetime` | Local wall-clock without storing offset in the value |
| `duration` | Elapsed time (not a clock instant) |

Primitives stay the **wire/SQL boundary** (`instant`, `local_date`, …). **Calendar-facing** shapes use stdlib aggregates below (and nest inside row UDTs).

**Forbidden as data-plane types:** `string` masquerading as `instant` / `local_date` on ports (display formatting belongs in **present**, or derived typed columns from `semantic_time`).

#### Aggregates (nested value types — not SQL `GROUP BY`)

**Aggregate** here means a **composite value type**: fields that are primitives or **other named types**, nested to arbitrary depth (still value-only, no references).

Authoring form (aligned with dashspec `block` / `end` style):

```text
type Address
  string Street
  string House
  string City
end type

type OrderDetail
  int OrderCode
  Address CustomerAddress
end type
```

| Rule | Meaning |
|------|---------|
| Field line | `<Type> <FieldName>` — type first, then field name (PascalCase types, camelCase or PascalCase fields per style guide at parse time) |
| Nesting | Field type may be any UDT or primitive |
| Nominal | `OrderDetail` is a **name**; two types with identical fields are still distinct if names differ |
| Flat rows | SQL-shaped rows are aggregates too — all fields primitives at top level (`StakeholderKpiRow`) |

**Field paths** (for diagram bindings, transform steps, Designer):

```text
CustomerAddress.House
OrderDetail.CustomerAddress.City
```

- Inside a **card `input`** typed `rows OrderDetail`, diagram properties use paths **relative to one row** (default: `CustomerAddress.House`).
- Fully qualified paths (`OrderDetail.CustomerAddress.House`) are allowed when the same path is reused across types or in global expressions.
- Modeling resolves paths at compile time; invalid path = error (unknown field, wrong kind for viz).

**Rowset on ports:** `rows OrderDetail` — a table whose rows are `OrderDetail` values. Channel/transform outputs use `rows <AggregateType>`.

**Surface syntax (normative):** all types use **`type` … `end type`** blocks ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) block family). **No** brace-literal type forms (`record { }`, `enum { }`, `list<>`) in authoring.

Optional SQL nullability: `optional string Notes` on a field line.

**No generic `list` or `enum` (v1):** “many values” for filters uses **`rows`** of a small UDT (see filter types below) or SQL/transform.

#### Fixed `array` (homogeneous vector)

One additional aggregate form for **fixed-length** component tuples (stdlib and advanced UDTs):

```text
type DateParts
  array int 3 YearMonthDay
end type
```

| Rule | Meaning |
|------|---------|
| Syntax | `array <primitive> <count> <FieldName>` — `count` is a compile-time constant |
| Element type | Primitives only (`int`, `bool`, …) — not `rows`, not nested `array` in v1 |
| Use | Rare; prefer named fields (`Year`, `Month`, `Day`). Vectors suit generated/stdlib glue |

No unbounded or runtime-sized arrays in v1.

#### Stdlib calendar aggregates (`<stdlib>/time.dashtype`)

First-class **named** value types (block-based) in the base library — authors `import` or reference without redefining:

```text
type Time
  int Hour
  int Minute
  int Second
end type

type Date
  int Year
  int Month
  int Day
end type

type Week
  int Year
  int Week
end type

type Month
  int Year
  int Month
end type

type Year
  int Year
end type
```

| Type | Role |
|------|------|
| **Time** | Wall-clock time of day (no date) |
| **Date** | Civil calendar day (reporting calendar, not UTC instant) |
| **Week** | ISO week bucket (week-number rules documented with `semantic_time` / calendar locale) |
| **Month** / **Year** | Reporting grain labels on axes and group-by transforms |

`semantic_time` and friends map `instant` / SQL datetimes → **`Date`**, **`Time`**, **`Date`+`Time`**, or grain types (`Week`, `Month`, `Year`) on typed columns — replaces string `dd.MM` on data ports.

Row example:

```text
type UsageDayRow
  Date UsageDay
  Time PeakTime
  int PeakConcurrentApps
end type
```

Modeling (F#) may use an internal `DashType` DU; that is **not** an alternate authoring syntax.

**Not in the type language:** SQL `GROUP BY` / `SUM` — that is a **transform step** producing a new `rows R'` aggregate type.

```text
type StakeholderKpiRow
  local_date UsageDay
  string UserSam
  int PeakConcurrentApps
end type
```

- Definitions live in `.dashtype`, module `types { }`, or stdlib (`lus.types`).
- SQL infer may propose a **flat** aggregate; authors may refactor to nested UDTs when the domain warrants it (no automatic nesting from dots in column names in v1).
- **SQL column → field:** channel `from view` maps `user_sam` columns to aggregate fields (`UserSam` or explicit `map user_sam → UserSam` in channel body when names differ).

#### Filter value types (separate from row types)

Filter ports use the same **`type` … `end type`** blocks (not generic collections):

```text
type UsageDateRange
  local_date From
  local_date To
end type

type SelectedAppNames
  string AppName
end type
```

`apply_filters` wiring: dashboard filter → port typed `UsageDateRange` or **`rows SelectedAppNames`** when the UI is multi-select `IN` (one row per chosen value — reuses `rows`, no `list`).

`top_n` and similar: either fields on a small filter UDT or a dedicated transform parameter — not a built-in `list`/`enum`.

Filter **application** lives in the **report internal flow** ([ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md) amendment), not inside channel nodes.

### 4. Refinement predicates (non-goals v1)

**Refinement predicates** (`string where non_empty`, `int where >= 0`) are **not** part of dashflow v1. The intended expressiveness in authoring is **primitives + algebraic aggregates** (§3), not a predicate calculus.

SQL column metadata (length, precision) may be stored as **mapping annotations** on inferred fields for Designer hints; that is not a separate type-level `where` syntax until a future ADR explicitly adds it.

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
| `decimal(p,s)` | `decimal` (p,s in mapping annotation) |
| `nvarchar(n)` / `varchar(n)` | `string` (n in mapping annotation) |
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
| **T0** | F# `DashType` DU (primitives + aggregates); port typing on `FlowGraph` |
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
