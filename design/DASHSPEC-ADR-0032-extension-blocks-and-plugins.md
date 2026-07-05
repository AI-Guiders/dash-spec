# DASHSPEC-ADR-0032: Extension blocks and plugins

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-07-05 |
| **Relates to** | [ADR-0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md), [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md), [ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md), [ADR-0008](DASHSPEC-ADR-0008-viz-render-plugins.md), [ADR-0013](DASHSPEC-ADR-0013-host-solid-ports-viz-registry.md), [ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md) |

## Context

Блочная грамматика ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md)) уже предполагает **block dispatcher**: новый keyword = новый handler в `ParseBlockBody`. Сейчас все handlers зашиты в Core/Host.

Параллельно:

- **Connectors** — DLL + TOML manifest (`[[plugins.load]]`), loader в Host.
- **Viz** — `IVizPlugin` + registry; рендер пока monolithic `switch` в Host ([ADR-0013](DASHSPEC-ADR-0013-host-solid-ports-viz-registry.md)).
- **Click** — bounded effects `show` / `set` / `goto` в Core parser ([ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md)); grammar растёт при каждом UX-variant.

Нужен способ добавлять **новые keyword-блоки и runtime-логику** (buttons, export, custom selection, …) **без PR в Core parser и без раздувания DSL mini-языком**. Хотим блок `logic` — пишем Logic Plugin; Host остаётся orchestration shell.

**Trade-off:** консистентность между product-specific extensions сложнее, чем у единого Core grammar. Это **осознанный выбор**: зоопарк plugin'ов — ответственность команды, которая их подключила; платформа даёт контракт, lint и discoverability, а не один mega-DSL.

## Decision

### Thin core, thick extensions

| Слой | Остаётся в Core (stable) | Extension plugin (DLL) |
|------|--------------------------|-------------------------|
| Document skeleton | `@kind id`, `runtime`, `configuration`, `wiring`, `report` | — |
| Data & query | `filter`, `bind`, `datasource`, diagram **kind registry** (`line`, `bar`, …) | optional `IDiagramKindExtension` (tier C, rare) |
| Navigation wiring | `set <filter> from x\|y\|value`, `goto tab\|page` | — |
| Presentation data | `presentation`, `transform series`, library presets | — |
| UX / actions | built-in defaults only | `buttons`, custom `on click`, `export`, … |
| Render backend | built-in viz ids | `IVizRenderer` / `IVizPlugin` ([ADR-0008](DASHSPEC-ADR-0008-viz-render-plugins.md)) |

Core **не** получает новые card-level keywords без ADR. Product teams добавляют **extension block plugins**.

### Extension block = keyword + schema + handler

DLL реализует **`IExtensionBlockPlugin`** (Abstractions):

```csharp
public interface IExtensionBlockPlugin
{
    /// <summary>Stable plugin id, e.g. "ursa-export".</summary>
    string Id { get; }

    /// <summary>DSL keyword this plugin owns, e.g. "buttons", "on click".</summary>
    string BlockKeyword { get; }

    /// <summary>Where the block may appear (Card, Report, Module, …).</summary>
    IReadOnlyList<ExtensionBlockScope> AllowedScopes { get; }

    /// <summary>Property/nested-block schema (same model as DiagramKindRegistry PropertySpec).</summary>
    IReadOnlyList<ExtensionBlockSpec> Schema { get; }

    /// <summary>Semantic validation after generic parse (cross-refs, required props).</summary>
    void Validate(ExtensionBlockNode node, ExtensionValidationContext ctx);

    /// <summary>Register host services, render fragments, action handlers.</summary>
    void Configure(IExtensionHostBuilder host);
}
```

**Generic parse:** Core `PropertyBlockParser` / block dispatcher читает `{ key = value … }` и вложенные `{ }` **без** plugin-specific tokenizer. Plugin задаёт **schema + semantics**, не произвольную грамматику.

**Rejected:** plugin-provided lexer/parser для свободного sub-DSL (CSX, expressions) — тот же риск mini-ЯП, что `.dashbehaviour` в [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md).

### Load vs use

| Phase | Where | Meaning |
|-------|-------|---------|
| **Load** (deployment) | TOML manifest from `runtime { manifest = "…" }` | DLL on disk, plugin id, optional block keywords |
| **Enable** (authoring) | `extensions { … }` in module | opt-in: какие loaded plugins активны для этого report |

#### TOML manifest (prod)

```toml
[[extensions.load]]
id = "ursa-interactions"
assembly = "Ursa.LicenseUsage.DashSpec.Interactions.dll"

[[extensions.load]]
id = "ursa-export"
assembly = "Ursa.LicenseUsage.DashSpec.Export.dll"
```

Paths resolve like connector plugins: `connectors/` / `extensions/` under Host base dir, or rooted path in manifest.

#### DSL module (author opt-in)

```text
@tab stakeholder {
  runtime { manifest = "lus-runtime.toml" }
  configuration { … }
  extensions {
    use ursa-interactions
    use ursa-export
  }
  wiring { … }
  report { … }
}
```

#### Dev override (local path)

```text
extensions {
  extension buttons import from "plugins/Ursa.DashSpec.Buttons.dll"
  use ursa-buttons
}
```

- **`import from "path"`** — dev / local override; **не** обязателен в prod spec.
- **`use <id>`** — enable plugin already loaded from manifest.
- Prod: только `use <id>`; arbitrary paths in committed `.dashspec` — **lint warning** (CI may fail).

Shorthand (optional v1.1):

```text
extensions {
  import buttons from "plugins/Buttons.dll"   # id inferred from plugin.Id
}
```

### Parse pipeline

```text
1. Read module skeleton → collect extensions { import | use }
2. Merge with TOML [[extensions.load]] → ExtensionPluginLoader (like ConnectorPluginLoader)
3. Build ExtensionBlockRegistry (keyword → plugin, id → plugin)
4. ParseBlockBody: known core keyword → core handler
                      known extension keyword → generic ExtensionBlockParser
                      unknown → error (+ hint: loaded extensions list)
5. Validate: core rules + plugin.Validate per ExtensionBlockNode
6. Compose IR → Host dispatches Configure() at startup + runtime handlers
```

If `extensions { import … }` appears **inside** module, loader runs **before** full parse of `report` (prepass on module body or two-pass). Tab modules that only `use` ids from manifest — single pass.

### Host role (minimal change per extension)

Host **не** ветвится `switch` на каждый новый block. Вместо этого:

| Concern | Host |
|---------|------|
| Load DLL | `ExtensionPluginLoader` |
| Registry | `ExtensionBlockRegistry` |
| Parse dispatch | Core calls registry |
| UI/runtime | `IExtensionHostBuilder` — register `RenderFragment`, `IInteractionHandler`, `IActionHandler` |
| Page orchestration | `DashboardPageController` invokes handlers through ports, not concrete types |

Новый block `buttons` → новая DLL + строка в manifest; **Host PR не нужен** (после v1 infrastructure).

Viz and action handlers may be registered **by the same extension plugin** or separate `IVizPlugin` / `IActionPlugin` implementations in one assembly.

### Relationship to ADR-0028 (`on click`)

[ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md) **core navigation** (`set`, `goto`) остаётся built-in — report wiring, lint без plugins.

**Presentation effects** (`show below list data from tooltip`, …) migrate to **built-in extension** `dashspec-on-click` (in-process, default enabled):

| v1 (today) | v2 (this ADR) |
|------------|---------------|
| `CardClickParser` hardcoded `show`/`set`/`goto` | `set`/`goto` — Core; `show`/`invoke` — extension handler |
| Grammar in Core | Same syntax; handler in `DashSpec.Extensions.OnClick` built-in DLL or in-process registration |

Products may **`use` custom interaction plugin** and replace default selection UX without new Core keywords.

Syntax evolution (optional, same ADR family as [ADR-0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md)):

```text
on click {
  invoke selection-list
  set user_name from y
  goto page detail
}
```

`invoke <handler-id>` — extension effect; maps to registered `IInteractionHandler`.

### Extension tiers (DX)

| Tier | Example | Core change | Plugin work |
|------|---------|-------------|-------------|
| **A** Action / chrome | `buttons { button export { … } }` | none | `IExtensionBlockPlugin` + `IActionHandler` |
| **B** Interaction | custom selection strip, drill panel | none | `IInteractionHandler` + optional block |
| **C** Viz backend | sparkline render | none | `IVizRenderer` ([ADR-0008](DASHSPEC-ADR-0008-viz-render-plugins.md)) |
| **D** Diagram kind | sankey bindings | **ADR + kind registry** | `IDiagramKindExtension` co-developed with Core |

Default product work is **A–C**. Tier D — explicit platform change, not «just a plugin».

### Consistency and the «zoo»

Platform **does not** guarantee uniform UX across arbitrary third-party extensions. It **does** guarantee:

1. **Keyword ownership** — one plugin per `BlockKeyword`; duplicate registration → startup error.
2. **Schema export** — each plugin embeds or ships `extension.manifest.json` (id, keyword, schema, version, abstractions semver).
3. **`dashspec lint`** — unknown block, missing `use`, schema violations, prod `import from` in git.
4. **`/dev/spec` + `/dev/capabilities` (dev)** — loaded plugins, keywords, handler ids.
5. **Abstractions semver** — plugins target `DashSpec.Abstractions` 1.x; Host/Core may stay 0.x.

**Governance (product team):**

- Prefer **one product extension assembly** (e.g. `Ursa.LicenseUsage.DashSpec`) over N tiny DLLs until needed.
- Shared blocks (`buttons`, `export`) — internal package, not copy-paste per report.
- «Сделали зоопарк — сами виноваты» — acceptable; lint + catalog of **approved** extension ids for prod CI.

### Security

- No `code = "…"` in spec.
- Prod: extension DLLs from manifest whitelist only; no arbitrary `import from` in committed specs.
- Plugin `Configure` runs at Host startup with same trust model as connector plugins ([ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md)).
- Hot reload extension DLL — **non-goal** v1 (same as connectors).

## Examples

### LUS — export button (tier A)

```text
@tab stakeholder {
  runtime { manifest = "lus-runtime.toml" }
  extensions { use ursa-export }
  report {
    card stakeholder_peak_apps {
      title = "№2 …"
      extensions {
        buttons {
          button csv {
            label = "Export CSV"
            on click run csv-export
          }
        }
      }
      on click { invoke selection-list }
      diagram lus_stakeholder_peak_apps_heatmap
      …
    }
  }
}
```

`buttons` block exists only when `ursa-export` (or a plugin registering keyword `buttons`) is enabled.

### Built-in default — no extra `use`

If Host registers built-in `dashspec-on-click`, cards keep:

```text
on click {
  show below list data from tooltip selectable
  set usage_date from x
  goto tab detail
}
```

until migrated to `invoke` form.

## Rejected

| Idea | Why |
|------|-----|
| Plugin custom tokenizer / CSX | mini-PL, lint nightmare |
| Unlimited keywords without `use` | silent dependency on whatever DLL host loaded |
| Every interaction in Core grammar | ADR-0028 growth path |
| `extension` blocks for `filter` / `datasource` | data layer stays Core for SQL compile + lint |

## Consequences

- **Abstractions:** `IExtensionBlockPlugin`, `ExtensionBlockRegistry`, `ExtensionBlockNode` IR, `IExtensionHostBuilder`, `IInteractionHandler`, `IActionHandler`.
- **Core:** block dispatcher consults registry; generic extension parse; prepass for `extensions { import }`.
- **Host:** `ExtensionPluginLoader`; thin dispatch from `DashboardPageController` / card chrome; built-in extensions registered in DI like today’s viz builtins.
- **Manifest:** `[[extensions.load]]` alongside `[[plugins.load]]`.
- **ADR-0028:** amended — `show`/`invoke` path via extension; `set`/`goto` remain core.
- **DX:** `dotnet new dashspec-extension`, sample spec, `extension.manifest.json`, lint rules, `/dev/capabilities`.

## Implementation plan

1. **Infrastructure** — Abstractions contracts, loader, registry, generic `ExtensionBlockParser`, IR node on `CardDefinition` / module.
2. **Host builder port** — `IExtensionHostBuilder`; migrate built-in `show` to in-process OnClick extension (behavior parity with ADR-0028 v1).
3. **Manifest + `extensions { use }`** — TOML + module block; prepass loader.
4. **First product plugin** — `Ursa.LicenseUsage.DashSpec.Export` (tier A) or interactions package.
5. **Lint + dev capabilities** — schema validation, prod `import from` rule.
6. **Viz renderer port** — align [ADR-0013](DASHSPEC-ADR-0013-host-solid-ports-viz-registry.md) `switch` with plugin `RenderFragment` (may ship parallel to step 2).

## Follow-up

- Umbrella architecture: [ADR-0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md) (plugin families, diagram vertical, unified loader).
- Extract shared `@behaviour` file for repeated `on click` — only if reuse > 2 cards **and** still core/extension ids, not freeform script.
