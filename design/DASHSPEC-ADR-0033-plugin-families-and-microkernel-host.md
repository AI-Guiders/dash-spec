# DASHSPEC-ADR-0033: Plugin families and microkernel host

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-07-05 |
| **Extends** | [ADR-0032](DASHSPEC-ADR-0032-extension-blocks-and-plugins.md) |
| **Relates to** | [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md), [ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md), [ADR-0003](DASHSPEC-ADR-0003-diagram-kinds-registry.md), [ADR-0008](DASHSPEC-ADR-0008-viz-render-plugins.md), [ADR-0013](DASHSPEC-ADR-0013-host-solid-ports-viz-registry.md), [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md) |

## Context

DashSpec накапливает extension points по частям:

| Сегодня | Где |
|---------|-----|
| Connectors | DLL + `[[plugins.load]]` ([ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md)) |
| Diagram kinds | hardcoded `DiagramKindRegistry` ([ADR-0003](DASHSPEC-ADR-0003-diagram-kinds-registry.md)) |
| Viz backends | `IVizPlugin` + monolithic `switch` ([ADR-0013](DASHSPEC-ADR-0013-host-solid-ports-viz-registry.md)) |
| Card blocks / click UX | Core parser + Host ([ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md)) |
| Extension blocks (proposed) | [ADR-0032](DASHSPEC-ADR-0032-extension-blocks-and-plugins.md) |

Каждый новый keyword или diagram kind тянет PR в Core/Host. Блочный dispatcher ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md)) уже готов к **регистрации handler'ов извне**, но модель «один интерфейс на всё» недостаточна.

Нужна **единая plugin-family архитектура**:

- **Host** — тонкий microkernel: load, capabilities, dispatch, page shell.
- **Spine plugins** — generic инфраструктура семейства (parse schema, payload contracts, shared UX).
- **Domain plugins** — конкретные kinds/blocks (line, heatmap, built-in selection).
- **Extension plugins** — product-specific слой **на hook'ах** domain/spine, без правки Host.

Product team может собрать «зоопарк» — платформа даёт контракт, lint, bundles; единый UX между arbitrary DLL **не обещаем**.

## Decision

### 1. Три слоя runtime

```
                    ┌─────────────────────────────┐
   Browser / MCP ──►│  DashSpec Host (microkernel) │
                    │  · plugin loader + bundles   │
                    │  · FilterState / session     │
                    │  · GET /dev/capabilities     │
                    │  · dispatch (render/click)   │
                    └──────────────┬──────────────┘
                                   │ loads
         ┌─────────────────────────┼─────────────────────────┐
         ▼                         ▼                         ▼
   Spine (in-proc ok)        Domain (built-in)         Extension (product DLL)
   diagram-spine             diagram-chart              card-export
   interaction-spine         diagram-matrix             card-export
   viz-spine                 on-click-default           sparkline-kind
   connector (existing)
```

| Слой | Ответственность | Меняет Host при добавлении? |
|------|-----------------|------------------------------|
| **Microkernel** | orchestration, registries merge, capabilities, thin Blazor shell | нет (после v1 infra) |
| **Spine** | generic contracts, shared parsers helpers, default dispatch | нет |
| **Domain** | reference kinds/blocks (line, heatmap, table, default `on click`) | нет — register at startup |
| **Extension** | новые kinds/blocks/handlers для продукта | нет — DLL + manifest |

### 2. Plugin families (не один `IPlugin`)

Семейство = **общий spine + hook-контракты + опциональные extension DLL**.

#### 2.1 Connector family (existing)

Без изменений [ADR-0001](DASHSPEC-ADR-0001-connectors-as-plugins.md): `IConnectorPlugin`, `IDataSourceConnector`, `[[plugins.load]]`.

#### 2.2 Diagram family

| Plugin id (example) | Role |
|---------------------|------|
| `diagram-spine` | `DataFamily` taxonomy, generic `diagram { }` parse dispatch, query-shape baseline |
| `diagram-chart` | kinds `line`, `bar`; `ChartPayload` builder; default `render=chartjs` |
| `diagram-matrix` | kind `heatmap`; `MatrixPayload` builder; default `render=css-grid` |
| `diagram-table` | kind `table` |
| `diagram-scalar` | kind `number` |
| `diagram-sparkline` (extension) | kind `sparkline`; reuses `DataFamily.Chart`; custom payload + renderer |

**Hook (Abstractions):**

```csharp
public interface IDiagramKindContributor
{
    string KindId { get; }
    DiagramDataFamily Family { get; }
    IReadOnlyList<PropertySpec> Schema { get; }
    void Validate(DiagramDefinition diagram, DiagramValidationContext ctx);
    object BuildPayload(IReadOnlyList<IDataRow> rows, DiagramDefinition diagram, DiagramBuildContext ctx);
    string? DefaultRenderPluginId { get; }
}
```

Core `DiagramKindRegistry` → **merged registry**: built-in contributors + loaded plugins. Unknown `diagram foo` → lint error + hint from capabilities.

**Query compile** остаётся в Core ([ADR-0003](DASHSPEC-ADR-0003-diagram-kinds-registry.md)): `bind` + filters → `SELECT` columns from bindings. Plugin декларирует required bindings; **не** произвольный SQL в spec.

Tier **D+** (custom query shape, pivot, multi-query) — отдельный `IQueryShapeContributor` + platform ADR; не default extension path.

#### 2.3 Viz family

| Plugin id | Role |
|-----------|------|
| `viz-spine` | `IVizPlugin` / `IVizRenderer` registry, resolve `render` from preset |
| `viz-chartjs`, `viz-css-grid`, … | render backends ([ADR-0008](DASHSPEC-ADR-0008-viz-render-plugins.md)) |

Domain diagram plugin may **bundle** viz renderer in same assembly or declare `DefaultRenderPluginId`.

Host `CardVisualization` → dispatch by `RenderPluginId` through registry (**не** hardcoded switch после migration).

#### 2.4 Interaction family

| Plugin id | Role |
|-----------|------|
| `interaction-spine` | click context types, `set`/`goto` core effects (Core parse), handler dispatch |
| `on_click_default` | built-in selection strip + `drill_down` (`show` / `invoke selection_list`) |
| `card_export` (extended) | `buttons` block + actions like `csv_export` |

**Hooks:**

```csharp
public interface IInteractionContributor
{
    string HandlerId { get; }
    bool TryHandle(ClickContext ctx, CardDefinition card, InteractionDispatch dispatch);
}

public interface ICardChromeContributor
{
    int Order { get; }
    RenderFragment Render(CardChromeContext ctx);  // buttons strip, badges, …
}
```

Core keeps **`set` / `goto`** ([ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md)) — report wiring, lint without plugins.

Presentation (`show`, `invoke`, custom panels) → interaction contributors ([ADR-0032](DASHSPEC-ADR-0032-extension-blocks-and-plugins.md)).

#### 2.5 Extension-block family

Card/module-level keywords (`buttons`, `export`, …) — детали в [ADR-0032](DASHSPEC-ADR-0032-extension-blocks-and-plugins.md): `IExtensionBlockPlugin`, generic property parse, `extensions { use }`.

Extension-block plugin may register **одновременно** `IExtensionBlockPlugin` + `IActionHandler` + `ICardChromeContributor`.

### 3. Spine + domain + extension pattern

**Правило product split** (как grouping в крупных plugin-systems):

1. **Domain plugin** владеет data + primary surface (diagram kind, default interaction).
2. **Extension plugin** вешается на **hook interface**; domain **не импортирует** extension.
3. **Без extension** — degraded mode OK (flat list, default selection, no export buttons).
4. **Soft dependency** — extension без domain consumer → loads, lint **warning**, no runtime error.

Пример bundle (capability ids, без product prefix):

```text
diagram-matrix
on_click_default
card_export
```

### 4. Единый контракт загрузки

Один loader facade (evolution of `ConnectorPluginLoader`):

```csharp
public interface IDashSpecPlugin
{
    string Id { get; }
    string DisplayName { get; }
    PluginTier Tier { get; }  // Core | Extended | Product
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
    void RegisterContributors(IDashSpecContributorRegistry registry);
}
```

`RegisterContributors` — merge into family registries:

- `registry.AddDiagramKind(…)`
- `registry.AddExtensionBlock(…)`
- `registry.AddVizRenderer(…)`
- `registry.AddInteraction(…)`
- `registry.AddCardChrome(…)`

Legacy: `IConnectorPlugin` remains; adapter wraps into same loader or parallel entry in manifest.

#### Manifest (TOML)

```toml
active_bundle = "lus-prod"

[[bundles]]
name = "demo"
plugins = ["sqlserver", "diagram-chart", "diagram-matrix", "diagram-table", "diagram-scalar", "on-click-default"]

[[bundles]]
name = "lus-prod"
plugins = ["sqlserver", "diagram-chart", "diagram-matrix", "diagram-table", "diagram-scalar",
           "on-click-default", "ursa-interactions", "ursa-export"]

[[plugins.load]]
id = "ursa-export"
assembly = "Ursa.LicenseUsage.DashSpec.dll"
tier = "product"

[[plugins.load]]
id = "sqlserver"
assembly = "DashSpec.Connector.SqlServer.dll"
tier = "core"
```

Paths: `plugins/`, `connectors/`, `extensions/` under Host base dir (unify to `plugins/` follow-up).

Env override: `DASHSPEC_PLUGIN_BUNDLE` (optional).

### 5. DSL: load vs use

| Phase | Where | Meaning |
|-------|-------|---------|
| **Load** | TOML `[[plugins.load]]` + bundle | DLL on disk |
| **Enable** | `extensions { use … }` in module | opt-in for this report |

```text
@tab stakeholder {
  runtime { manifest = "lus-runtime.toml" }
  extensions {
    use ursa-interactions
    use ursa-export
  }
  report {
    card peak {
      diagram heatmap { … }
      on click { invoke selection-list; set user_name from y; goto page detail }
      buttons {
        button csv { label = "Export"; on click run csv-export }
      }
    }
  }
}
```

Dev-only path import ([ADR-0032](DASHSPEC-ADR-0032-extension-blocks-and-plugins.md)):

```text
extensions {
  import ursa-export from "plugins/Ursa.LicenseUsage.DashSpec.dll"
  use ursa-export
}
```

### 6. Parse pipeline (block dispatcher + registries)

```text
1. Resolve bundle → load DLLs → RegisterContributors → merged registries
2. Prepass module: extensions { import | use }
3. ParseBlockBody(keyword):
     core keyword     → Core handler
     extension block  → ExtensionBlockParser (schema from plugin)
     diagram <kind>   → DiagramKindRegistry (core + contributors)
4. Validate: Core + per-plugin Validate
5. Compose IR (ExtensionBlockNode, DiagramDefinition, …)
6. Runtime: Host dispatches render/click/chrome via contributor registries
```

Parser **не** получает plugin-specific tokenizer. Plugin = **schema + semantics + handlers**.

### 7. Capabilities (discoverability)

Dev-only (like `/dev/spec`):

`GET /dev/capabilities`

```json
{
  "bundle": "lus-prod",
  "plugins": [
    { "id": "diagram-matrix", "tier": "core", "kinds": ["heatmap"], "families": ["matrix"] },
    { "id": "ursa-export", "tier": "product", "blocks": ["buttons"], "actions": ["csv-export"] }
  ],
  "diagramKinds": ["line", "bar", "heatmap", "table", "number"],
  "extensionBlocks": ["buttons"],
  "interactionHandlers": ["selection-list", "csv-export"],
  "vizRenderers": ["chartjs", "css-grid", "table-html", "scalar-html"]
}
```

Lint и `/dev/spec` используют тот же snapshot.

### 8. Thin core invariant (не выносим в plugins без ADR)

| Concern | Owner |
|---------|--------|
| Document skeleton (`@kind`, `runtime`, `wiring`, `report`) | Core |
| `filter`, `bind`, `datasource`, `QueryCompiler` baseline | Core |
| `set … from x\|y\|value`, `goto tab\|page` | Core |
| `presentation` / library presets merge | Core |
| `DataFamily` enum taxonomy (small) | Core + ADR when new family |
| Arbitrary SQL / expressions in spec | **Rejected** |

### 9. Bundles and tiers

| Tier | Meaning | Examples |
|------|---------|----------|
| **core** | shipped with Host; required for reference samples | diagram-*, on-click-default, sqlserver |
| **extended** | optional platform plugins | postgres connector, sparkline |
| **product** | customer/product DLL | ursa-* |

**Bundle** = which plugins load on instance. **Tier** = lint/CI policy (product plugins: approved id list in CI).

### 10. Consistency and governance

Platform guarantees:

1. **Unique ownership** — one plugin per `KindId`, `BlockKeyword`, `HandlerId`; duplicate → startup fail.
2. **Schema export** — `dashspec.plugin.json` embedded or sidecar (id, tier, kinds, blocks, abstractions version).
3. **`dashspec lint`** — unknown kind/block, missing `use`, schema violation, `import from` in committed prod spec.
4. **Capabilities snapshot** — baseline test in CI (drift detection).
5. **`DashSpec.Abstractions` semver** — plugins target stable 1.x API.

Product team discipline:

- Prefer **one product assembly** (`Ursa.LicenseUsage.DashSpec.dll`) over many tiny DLLs.
- Zoo is opt-in via bundle + `extensions { use }` — «сделали зоопарк — сами виноваты».

Future: Roslyn analyzers `DSPEC001…` (plugin references only Abstractions+Sdk; no Host internals) — follow-up.

### 11. Vertical diagram package (SDK)

`dotnet new dashspec-diagram` scaffolds:

- `IDiagramKindContributor`
- optional `IVizRenderer`
- optional `IInteractionContributor`
- test `.dashdiagram` + payload golden test

One assembly may export **full vertical slice** (kind + render + click) for product teams.

## Rejected

| Idea | Why |
|------|-----|
| Single mega-`IPlugin` with all methods | violates family separation; forces Host knowledge |
| Plugin custom grammar / CSX | mini-PL ([ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md)) |
| Domain plugin hard-depends on extension | breaks optional product layer |
| Auto-load all DLLs in folder without manifest | undeclared surface, security |
| Move filters/datasource to plugins | breaks SQL lint and Core compile |

## Consequences

- **Abstractions:** `IDashSpecPlugin`, `IDashSpecContributorRegistry`, family hooks (`IDiagramKindContributor`, `IInteractionContributor`, `ICardChromeContributor`, `IExtensionBlockPlugin`, `IVizRenderer`).
- **Core:** merged `DiagramKindRegistry`; block dispatcher consults extension registry; prepass `extensions { }`.
- **Host:** unified `DashSpecPluginLoader`; thin `DashboardPageController` dispatch; built-in domain plugins registered in DI (same as today, structured as contributors).
- **ADR-0032:** extension-block details unchanged; this ADR — umbrella architecture.
- **ADR-0028:** `set`/`goto` core; presentation via interaction family.
- **ADR-0013 / 0008:** viz `switch` → registry dispatch.

## Implementation plan

| Step | Deliverable |
|------|-------------|
| 1 | `IDashSpecContributorRegistry` + in-process built-in contributors (parity with current line/bar/heatmap/table/number) |
| 2 | Unified plugin loader + `[[plugins.load]]` tier + bundles in TOML |
| 3 | `GET /dev/capabilities` |
| 4 | Extension-block infra ([ADR-0032](DASHSPEC-ADR-0032-extension-blocks-and-plugins.md) step 1–3) |
| 5 | Interaction spine: migrate built-in `show` → `on-click-default` contributor |
| 6 | Viz registry: `CardVisualization` dispatch via `IVizRenderer` |
| 7 | First product plugin: `Ursa.LicenseUsage.DashSpec` (export + optional drill) |
| 8 | `dotnet new dashspec-diagram`, `dotnet new dashspec-extension`, lint rules |

## Follow-up

- Single `plugins/` directory; deprecate separate `connectors/` path alias.
- `dashspec.plugin.json` JSON Schema.
- `DSPEC001` analyzer package.
- `IQueryShapeContributor` when first non-SELECT diagram appears.
