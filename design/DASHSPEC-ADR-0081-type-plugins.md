# DASHSPEC-ADR-0081: Semantic types as plugins

| | |
|---|---|
| **Status** | Accepted (concept; loader phased) |
| **Date** | 2026-10-02 |
| **Relates to** | [ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md), [ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md), [ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md), [ADR-0082](DASHSPEC-ADR-0082-dashspec-sdk.md) |

## Context

[ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md) defines a **closed stdlib** (`int`, `Date`, `DateTime`, …) and **author-defined aggregates** (`type` … `end type`). That is enough for most row shapes and graph wiring.

Some domains need **value algebra** that must not grow into the DashSpec DSL: money with currency rules, opaque ids with validation, geo, industry codes, custom temporal calendars, encrypted tokens, etc. The same anti-pattern as transforms — a **type expression language** inside `.dashflow` / `.dashtype` — would recreate a general-purpose platform.

Connectors ([ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md)) and transforms ([ADR-0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md)) already use **plugins + manifest id**. Types can follow the same symmetry.

## Decision

### Two layers (do not collapse them)

| Layer | What DashSpec owns | What plugins own |
|-------|-------------------|------------------|
| **Structural** | `type` … `end type` blocks, field paths, `rows R`, port wiring, compile-time graph | — |
| **Semantic** | **Surface contract** only: type **name** (id), **properties** visible to bindings, optional **parameters** on the type ref | Parsing, validation, comparison, SQL/CLR mapping, DST/calendar logic, hashing for group-by |

**Default path:** plain aggregates in spec (0079). **Escape hatch:** `type use <plugin_type_id>` (or `type Money from ursa.money`) when behavior is non-trivial.

Stdlib primitives (`Date`, `Time`, `UtcOffset`, …) ship as **builtin type plugins** with fixed ids — same pattern as builtin transform `to_zone`. Authors still **import** stdlib names; implementation is not special-cased in the parser beyond registry lookup.

### Authoring surface (contract)

DashSpec exposes only what Modeling and Designer need **without** executing arbitrary user code at compile time:

| Surface | Role |
|---------|------|
| **Type id** | Registry key (`stdlib.date`, `ursa.money`) |
| **Properties** | Named fields for diagram bindings and transform port schemas (`Amount`, `Currency`) |
| **Property paths** | Nested paths when plugin declares composite shape (`Amount.Scale`) |
| **Methods** | **Not** callable from spec expressions in v1. Methods exist on the **plugin API** for Execution / transforms (e.g. `Normalize()`, `ToReportingCurrency()`). If a method must appear in authoring, it is a **transform** (`transform use`) or a **named plugin operation** in manifest — not `row.Amount.Normalize()` in DSL. |
| **Literals** | Plugin-defined literal syntax in **block params only** (like transform params): `currency = USD`, not arbitrary expressions |

```text
import types from ursa.types

channel kpi from sql.view {
  output rows KpiRow
}

type KpiRow
  ursa.money Revenue
  Date UsageDay
end type
```

Or sugar:

```text
type Revenue from ursa.money
```

Field lines then reference **plugin types** wherever a primitive or UDT name is allowed on a port.

### Plugin contract (sketch)

```csharp
public interface ITypePlugin
{
    string Id { get; }
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}

/// <summary>Metadata + behavior for one named value type.</summary>
public interface IValueType
{
    string Id { get; }
    /// <summary>Fields/kinds for Modeling — must be available without running user queries.</summary>
    TypeManifest Manifest { get; }
    bool TryParseLiteral(string literal, out object? value, out string? error);
    bool IsAssignableTo(IValueType target, AssignmentContext ctx);
    // Execution: compare, hash, map from SqlCell, map to parameter, etc.
}
```

- **`TypeManifest`** is the SSOT for Designer schema preview and edge compatibility (like `TransformSignature`).
- Modeling (F#) **queries manifest** for graph check; may call **pure** plugin hooks for assignability (`ursa.money` → `decimal` only via explicit transform, not implicit coercion).

Loading: same `[[plugins.load]]` as connectors/transforms; registry merges builtin + external.

### What stays strict (0079 unchanged in spirit)

| Still true | Reason |
|------------|--------|
| No `object` / `json` on ports | Plugins declare concrete manifests |
| No user-defined functions in spec | Logic in plugin dll or SQL view |
| Value-only | Plugin types are still values; no shared mutable handles |
| Compile-time wiring | Mismatch = diagnostic; plugin cannot opt out of manifest |

### DSL power budget (types)

| In spec | In type plugin |
|---------|----------------|
| Name type on field line, `type use`, literals in params | Validation, parsing, locale, crypto, odd SQL types |
| Property paths for bindings | Internal representation |
| `rows R` where `R` mixes stdlib + plugin fields | Row materialization at connector boundary |

Promote to **SQL view column** or **builtin stdlib type** when the domain is universal; keep **product plugin** for one-off semantics.

### Author personas (honest split)

| Persona | Typical work | Writes C#/dll? |
|---------|----------------|----------------|
| **Report author** | `.dashspec`, cards, filters, channel `from view`, graph wiring with **builtin** transform ids | No |
| **Power author** | `.dashtype`, `.dashflow`, nested UDTs, report internal graph, runtime `[[plugins.load]]` of **vendor-shipped** packages | No — but needs platform literacy (manifest ids, signatures) |
| **Platform / product dev** | New connectors, transforms, semantic types; promote recurring patterns into **stdlib builtins** | Yes |

**Target:** ~**95%** of dashboards never load a custom type plugin. They use stdlib (`Date`, `Time`, `DateTime`, filters-as-UDT), builtin transforms (`to_zone`, `apply_filters`, `project`), and **SQL views** for heavy shaping. The remaining ~5% is product-specific semantics (money rules, proprietary codes) — that author is effectively a **developer** or consumes a dll built by the platform team.

Plugins are an **escape hatch**, not the default authoring path. Success metric for the stdlib: each release shrinks the 5% by promoting stable patterns to builtins (same as connectors and transforms).

## Phased delivery

| Phase | Deliverable |
|-------|-------------|
| **T0** | `ITypePlugin` / `IValueType` + `TypeManifest` in Abstractions; stdlib types registered as builtins |
| **T1** | Modeling reads manifests for port check + path validation |
| **T2** | Connector SQL infer proposes plugin types via manifest hints |
| **T3** | External `[[plugins.load]]` types; Designer shows plugin property trees |

## Non-goals

- Turning every `type CustomerAddress` block into a plugin (structural UDT stays in spec).
- Method calls or expressions on values in `.dashflow` / `.dashspec`.
- Reference types, ORM entities, or plugin types that hide their field list from Modeling.

## Consequences

- **Trilogy:** connector (source) → channel (rows) → transform (row ops) → **type plugin (value meaning)** — symmetric mental model for platform extenders.
- Complex logic concentrates in normal languages (C#/F#); DashSpec remains **wiring + types-as-names + manifests**.
- Versioning: breaking manifest changes are **dll major** events; locked `.dashtype` / channel output declarations detect drift at compile time.
