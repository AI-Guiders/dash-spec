# DashSpec plugins

Host loads **capability plugins** — export, drill-down, selection chrome, extra diagram kinds — through `dash-spec.toml` and optional module opt-in (`extensions { use … }`). See [ADR-0033](../design/DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md) and [ADR-0032](../design/DASHSPEC-ADR-0032-extension-blocks-and-plugins.md).

Plugins are **not** tied to a product or customer name. A plugin registers **handlers** and **blocks** by capability id:

| Kind | Example ids | Role |
|------|-------------|------|
| **Action handler** | `csv_export` | Button / chrome action (export, download, …) |
| **Interaction handler** | `selection_list`, `drill_down` | Click UX (list below cell, filter+navigate, …) |
| **Phrase template** | `export card as {format} …` | SpecFlow-style line in `on click { }` |
| **Diagram / viz** | `heatmap`, `chartjs` | Data family + renderer |

Core keeps thin wiring (`set … from`, `goto tab|page`). Presentation and chrome come from plugins.

## Quick start

1. Implement `IDashSpecPlugin` (references **only** `DashSpec.Abstractions`).
2. Register contributors: blocks, action handlers, interaction handlers, …
3. Add `[[plugins.load]]` + bundle entry in TOML.
4. Opt in per module:

```text
@tab overview {
  runtime { manifest = "runtime.toml" }
  extensions {
    use card_export
  }
  report "Overview" {
    card peak {
      on click { invoke drill_down; set user from y; goto tab detail }
      buttons {
        export { label = "CSV"; action = csv_export }
      }
    }
  }
}
```

Use **underscores** in ids (`card_export`, `drill_down`, `csv_export`). Unquoted hyphens in DSL are parsed as relative-day suffixes.

## Bundles and tiers

| Tier | Meaning |
|------|---------|
| `core` | Always loaded (`on_click_default`, `viz_builtin`, …) |
| `extended` | Optional capability DLLs (`card_export`, …) |
| `product` | Rare third-party / bespoke DLLs (same contract, your bundle) |

`active_bundle` in TOML or `DASHSPEC_PLUGIN_BUNDLE` env.

## Shipped with this repo

| Plugin id | Tier | Handlers / blocks |
|-----------|------|-------------------|
| `diagram_builtin` | core | diagram kinds: line, bar, heatmap, … |
| `on_click_default` | core | `selection_list`, `drill_down` |
| `viz_builtin` | core | chartjs, css-grid, table-html, scalar-html |
| `card_export` | extended | block `buttons`; action `csv_export` |
| `card_views` | extended | block `views`; action `switch_view`; segmented diagram toggle |
| `dashspec_diagnostics` | extended | HTTP `/diagnostics/*` — load timings, connector ping, capabilities, UI load trace |

## Author diagnostics (`dashspec_diagnostics`)

Opt-in via bundle (no `extensions { use … }` in spec — host-level only). Useful for spec authors and ops when a dashboard hangs on load.

Add to runtime TOML `[[plugins.bundles]]` (must be under `[plugins]`, not root `[[bundles]]`):

```toml
[plugins]
active_bundle = "lus-dev"

[[plugins.bundles]]
name = "lus-dev"
plugins = [..., "dashspec_diagnostics"]
```

| Endpoint | Description |
|----------|-------------|
| `GET /diagnostics/load` | Timed pipeline: parse, library, field options, optional card SQL (`?cards=true`, `?entry=overview`) |
| `GET /diagnostics/load/ping` | Connector smoke test |
| `GET /diagnostics/load/last` | Last Blazor UI load trace |
| `GET /diagnostics/load/history` | Recent UI load traces |
| `GET /diagnostics/capabilities` | Merged plugin capabilities (same as former `/dev/capabilities`) |

Implement `IDashSpecEndpointContributor` to expose HTTP from a plugin DLL; Host maps routes after startup via `MapPluginEndpoints`.

## Card chrome (`views`, `buttons`)

Host renders extension blocks registered with `AddCardChrome` — plugin declares block schema + action handlers; UI lives in Host (`CardExtensionChrome`).

```text
extensions { use card_views }

card peak {
  views {
    default = heatmap
  views {
    default = heatmap
    line { label = "Line"; diagram = peak_line }
    heatmap { label = "Heatmap"; diagram = peak_heatmap }
  }
  }
  diagram peak_heatmap
  …
}
```

## Filter widgets

Core parses `filter … widget chips|combobox|select|day|range|top`. Host `FilterWidgetRegistry` maps widget id → renderer component. Register via `AddFilterWidget` (built-in `filter_widgets_builtin` ships combobox, select, chips, day, range, top).

```text
filter field app_name on dbo.v.app as "Products" widget chips
```

`GET /diagnostics/capabilities` (with `dashspec_diagnostics` in bundle) lists the merged registry.

## Authoring

```csharp
public sealed class CardExportPlugin : IDashSpecPlugin
{
    public string Id => "card_export";
    public PluginTier Tier => PluginTier.Extended;

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        registry.AddExtensionBlock(new ExtensionBlockContributorDescriptor(
            Id, "buttons", ["Card"], ["label", "action"]));
        registry.AddActionHandler(new ActionHandlerDescriptor(
            Id, "csv_export", "Export card rows as CSV"));
    }
}
```

Scaffold: `dotnet new dashspec-extension -n MyOrg.CardExport`

Copy DLL → Host `plugins/`, add manifest + bundle. No product branding required in ids — name the **capability**.

## Phrase templates (ADR-0034)

Plugin registers a **pattern + slots** for a document scope (e.g. `card.on_click`):

```csharp
registry.AddPhraseTemplate(new PhraseTemplateDescriptor(
    Id,
    "csv_export",
    PhraseScopes.OnClick,
    "export card as {format} with delimiter {delimiter}",
    [
        new PhraseSlotDescriptor("format", PhraseSlotKind.Ident),
        new PhraseSlotDescriptor("delimiter", PhraseSlotKind.String),
    ]));
```

Spec:

```text
on click {
  export card as csv with delimiter ";"
  invoke drill_down(from = y)
}
```

Document scopes (`card`, `card.on_click`, …) — Core skeleton; `scope_builtin` exports metadata to `/dev/capabilities`. See [ADR-0034](../design/DASHSPEC-ADR-0034-phrase-templates-and-scopes.md).

## Action handlers (runtime)

Register handler metadata + optional `IDashSpecActionHandler` implementation:

```csharp
services.AddSingleton<IDashSpecActionHandler, CsvExportActionHandler>();
registry.AddActionHandler(new ActionHandlerDescriptor(Id, "csv_export", "Export card data as CSV"));
```

Chrome on card:

```text
extensions { use card_export }

buttons {
  export { label = "CSV"; action = csv_export }
}
```

Host `DashSpecActionDispatcher` executes the handler and can trigger browser download (table / matrix / chart → CSV). Unknown `action` / `invoke` ids fail at parse when Host supplies `KnownActionHandlers` / `KnownInteractionHandlers`.
