# DASHSPEC-ADR-0080: Dataflow transforms as plugins

| | |
|---|---|
| **Status** | Accepted (concept; loader phased) |
| **Date** | 2026-10-02 |
| **Relates to** | [ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md), [ADR-0078](DASHSPEC-ADR-0078-dashflow-data-plane.md), [ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md), [ADR-0081](DASHSPEC-ADR-0081-type-plugins.md), [ADR-0082](DASHSPEC-ADR-0082-dashspec-sdk.md) |

## Context

Dashflow needs transform steps (zone, grain, join, custom ETL). A growing **expression language inside `.dashspec`** would turn the DSL into a general-purpose language (anti-goal; see discussion on “1C-shaped” platforms).

Connectors already use **plugins + manifest** ([ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md)): spec names an id; Host loads dll from `connectors/` (or `[[plugins.load]]` in runtime TOML).

## Decision

### Same pattern as connectors

| Layer | Responsibility |
|-------|----------------|
| **DashSpec.Abstractions** | `ITransformPlugin`, `IDataFlowTransform`, port signature metadata, `TransformRegistry` |
| **DashSpec.Execution.Runtime** | invoke transforms on typed row batches, cache keys |
| **DashSpec.Transform.*** / product dlls | optional dll under `transforms/` (or shared `plugins/`) |
| **Builtins** | shipped steps (`to_zone`, `project`, `apply_filters`, …) are **plugins with fixed ids**, not special-case interpreter opcodes |

### Contract (sketch)

```csharp
public interface ITransformPlugin
{
    string Id { get; }
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}

public interface IDataFlowTransform
{
    string Id { get; }
    /// <summary>Declared input/output port names and DashType rows schemas — validated at compile time.</summary>
    TransformSignature Signature { get; }
    Task<TransformResult> ExecuteAsync(TransformContext context, CancellationToken cancellationToken);
}
```

- **Inputs / outputs** are typed ([ADR-0079](DASHSPEC-ADR-0079-dashflow-type-system.md)): e.g. `in: rows UtilizationRow` → `out: rows UtilizationRowLocalized`.
- Plugin **cannot** bypass Modeling: graph wiring must match `Signature` (or plugin declares a generic signature + schema validation at first run — prefer static signature in plugin manifest).

### Spec surface

**Wiring** (runtime TOML, like connectors):

```toml
[[plugins.load]]
id = "demo_custom_transforms"
path = "transforms/Lus.Custom.dll"
```

**Flow** (`.dashflow` / report internal graph):

```text
transformer localize {
  input raw from channel_util.out
  transform use to_zone
  zone = Europe/Moscow
  output localized
}

transformer custom_peak {
  input raw from channel_util.out
  transform use demo.peak_enrichment
  output enriched
}
```

| Form | When |
|------|------|
| `transform use <plugin_id>` | Registered transform (builtin or loaded dll) |
| `transform use "<relative/path.dll>"` | **Discouraged** except dev; prod uses manifest id only |

Optional block sugar (same id):

```text
transform use to_zone {
  zone = Europe/Moscow
}
```

Parameters are **plugin-defined** (TOML-like key/value in block); no arbitrary expressions in spec — only literals and references to report params / filter ports.

### Builtins vs custom

| Kind | Examples | Ship |
|------|----------|------|
| **Builtin plugins** | `to_zone`, `to_grain`, `apply_filters`, `project`, `join` | In `DashSpec.Execution.Runtime` or `DashSpec.Transform.Builtins` |
| **Product plugins** | demo-specific enrichment, odd joins | Customer / `demo.*` dll |

Renaming: there is **no special node type `semantic_time`** — only `transform use to_zone` (or equivalent id).

### DSL power budget

| In spec | In plugin (C#/F#) |
|---------|-------------------|
| Wire ports, `transform use`, literal params | Algorithms, IO, complex joins |
| Typed `rows R` | Map rows, allocate new schema |
| Closed manifest of plugin ids | Full language |

If logic is stable and shared → promote to **SQL view** or **builtin** transform; one-off → plugin.

**Coverage goal:** most report authors only ever use **builtin** transform ids; custom dll is platform-team / product-dev territory ([ADR-0081](DASHSPEC-ADR-0081-type-plugins.md) personas).

## Phased delivery

| Phase | Deliverable |
|-------|-------------|
| **P0** | This ADR + registry interface in Abstractions |
| **P1** | Builtin `to_zone`, `apply_filters` as plugins; graph compile checks signatures |
| **P2** | `[[plugins.load]]` for external transforms; Studio shows plugin port types |

## Non-goals

- Executing arbitrary C# from spec strings.
- Transform plugins that open their own SQL connections (use **channel** + connector instead).

## Consequences

- Escape hatch is **explicit** (`transform use`), reviewable, versioned dll — not DSL creep.
- Data Flow Designer nodes = **plugin id + params**, schema from `Signature`.
- Symmetry with connectors lowers platform cognitive load.
