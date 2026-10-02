# DASHSPEC-ADR-0079: Dashflow type system — strict static, value-only

| | |
|---|---|
| **Status** | Accepted (normative for dashflow; phased implementation) |
| **Date** | 2026-10-02 |
| **Relates to** | [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md), [ADR-0006](DASHSPEC-ADR-0006-sql-datasource-and-sqldialect.md), [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) |

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
| `array primitive N` on a field line | Unbounded or nested arrays (v1) |
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
| `duration` | Elapsed time (not a clock reading) |

**Time in DashSpec — one family, no `instant` primitive**

There is **no** separate type for “UTC facts” vs “local display”. Everything is stdlib **`Date`**, **`Time`**, **`DateTime`** with **`UtcOffset`** on the value:

| `UtcOffset.TotalMinutes` | Meaning |
|--------------------------|---------|
| **0** | UTC frame (what SQL stores as `datetime2` / `occurred_at_utc` — clock reads **in UTC**) |
| **≠ 0** | Same types, civil frame shifted (e.g. reporting zone after builtin `to_zone` — [ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md)) |

So **“UTC+0” is not another primitive** — it is **`Time` / `Date` / `DateTime` with `Offset` zero**. “Local” is the **same types** with another offset (and fields adjusted when converting).

No `local_date`, `local_datetime`, or `instant` in the type language.

**Forbidden as data-plane types:** `string` masquerading as time on ports (display formatting belongs in **present** only).

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
type UtcOffset
  int TotalMinutes
end type

type Time
  int Hour
  int Minute
  int Second
  UtcOffset Offset
end type

type Date
  int Year
  int Month
  int Day
  UtcOffset Offset
end type

type DateTime
  Date Day
  Time Clock
end type

type Week
  int Year
  int WeekNumber
end type

type Month
  int Year
  int MonthNumber
end type

type Year
  int Year
end type
```

| Type | Role |
|------|------|
| **UtcOffset** | Fixed offset from UTC (`TotalMinutes`; `0` = UTC civil frame). IANA zone → offset resolved in **`transform use to_zone`** (DST rules live in the plugin, not in every row) |
| **Time** | Time of day in the frame of **`Offset`** (often paired with `Date`; alone for `time`-only SQL columns) |
| **Date** | Calendar day in the frame of **`Offset`** |
| **DateTime** | **`Date` + `Time`** (same idea as .NET `DateTime` ≈ `DateOnly` + `TimeOnly`, but nested UDTs). Modeling **requires** `Day.Offset` = `Clock.Offset` |
| **Week** | ISO week bucket in that calendar frame |
| **Month** / **Year** | Reporting grain labels on axes and group-by transforms |

The **`to_zone`** transform rewrites **`Date` / `Time` / `DateTime`** fields: same types, new `Offset` and component values for the reporting zone. Converting **UTC+0 → Moscow** is not a type change.

Row fields from SQL UTC typically use **`DateTime`** (`Day` and `Clock` both **`Offset` 0**) on the channel; after **`to_zone`**, a row may expose only **`Date UsageDay`** or **`Time PeakTime`** with reporting offset.

#### Parallel to .NET (conceptual, not naming SSOT)

| .NET (modern) | DashSpec stdlib |
|---------------|-----------------|
| `DateOnly` | `Date` (+ `UtcOffset` on the value) |
| `TimeOnly` | `Time` (+ `UtcOffset`) |
| `DateTime` (date + time, no offset in the struct) | `DateTime` = nested **`Date Day`** + **`Time Clock`** |
| `DateTimeOffset` (instant + fixed offset) | Same **`DateTime`** when `Day.Offset` = `Clock.Offset`; zone/DST via **`to_zone`**, not a second primitive |

DashSpec does **not** use CLR type names in spec; Execution maps aggregates at the connector boundary.

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
  Date UsageDay
  string UserSam
  int PeakConcurrentApps
end type
```

- Definitions live in `.dashtype`, module `types { }`, or stdlib (`demo.types`).
- SQL infer may propose a **flat** aggregate; authors may refactor to nested UDTs when the domain warrants it (no automatic nesting from dots in column names in v1).
- **SQL column → field:** channel `from view` maps `user_sam` columns to aggregate fields (`UserSam` or explicit `map user_sam → UserSam` in channel body when names differ).

#### Filter value types (separate from row types)

Filter ports use the same **`type` … `end type`** blocks (not generic collections):

```text
type UsageDateRange
  Date From
  Date To
end type

type SelectedAppNames
  string AppName
end type
```

`apply_filters` wiring: dashboard filter → port typed `UsageDateRange` or **`rows SelectedAppNames`** when the UI is multi-select `IN` (one row per chosen value — reuses `rows`, no `list`).

`top_n` and similar: either fields on a small filter UDT or a dedicated transform parameter — not a built-in `list`/`enum`.

Filter **application** lives in the **report internal flow** ([ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md) amendment), not inside channel nodes.

### 4. Refinement predicates (non-goals v1)

**Refinement predicates** (`string where non_empty`, `int where >= 0`) are **not** part of dashflow v1. The intended expressiveness in authoring is **primitives + algebraic aggregates** (§3), not a predicate calcudemo.

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
| `date` | `Date` (`Offset` 0 from SQL unless annotated) |
| `datetime2`, `datetimeoffset` (UTC normalized) | `DateTime` (`Offset` 0) |
| `time` | `Time` (`Offset` 0) or `duration` per column role |

Ambiguous columns (untyped `sql` ad-hoc) **must** be annotated in channel output before graph wiring.

#### DashSpec → .NET (Execution.Runtime)

| DashSpec | .NET (normative wire) |
|----------|------------------------|
| `bool` | `bool` |
| `int` | `int` |
| `decimal` | `decimal` |
| `string` | `string` |
| `Date` / `Time` / `DateTime` / `UtcOffset` | Execution maps to `DateOnly` / `TimeOnly` / `DateTime` or `DateTimeOffset` per field rules; composed `DateTime` expands to day + clock with matching offset |
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
to_zone: (rows R, zone, …) → rows R'
```

where `R'` is computed by Modeling rules (e.g. `DateTime` UTC+0 → `Date`/`Time` with reporting `UtcOffset`). Invalid step chain = compile error.

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
- demo / view drift detected when locked UDT ≠ inferred SQL schema.
