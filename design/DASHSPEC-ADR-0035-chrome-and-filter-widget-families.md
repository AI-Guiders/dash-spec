# DASHSPEC-ADR-0035: Card chrome and filter widget families

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-07-05 |
| **Extends** | [ADR-0032](DASHSPEC-ADR-0032-extension-blocks-and-plugins.md), [ADR-0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md), [ADR-0034](DASHSPEC-ADR-0034-phrase-templates-and-scopes.md) |

## Context

Extension plugins can declare blocks (`buttons`) and action handlers, but Host chrome was hardcoded. Filter `widget = …` was parsed in Core while UI lived in Host components.

Need: **declarative controls** on cards (view switch, export, …) and **pluggable filter widgets** without moving filter semantics / SQL compile to plugins.

## Decision

### 1. Card chrome family

| Piece | Owner |
|-------|--------|
| Extension block keyword (`views`, `buttons`) | Plugin registers via `AddExtensionBlock` |
| Block schema / lint | Plugin descriptor |
| **Chrome UI** | Host `ExtensionChromeRenderer` maps keyword → render kind (`buttons`, `view_switch`) |
| **State + actions** | `IDashSpecActionHandler` + optional `ICardViewState` (Abstractions) |

```text
views {
  default = heatmap
  view line { label = "Линия"; diagram = lus_peak_concurrent_line }
  view heatmap { label = "Heatmap"; diagram = lus_peak_concurrent_heatmap }
}
```

`switch_view` action sets card-local view id → Host re-renders card with diagram preset from active `view`.

Core `CardViewSwitchApplier` swaps `diagram` preset id before `CardDiagramResolver`.

### 2. Filter widget family

| Piece | Owner |
|-------|--------|
| `filter field … widget chips` grammar | Core (unchanged) |
| SQL / bind / compile | Core |
| Widget id registry | Abstractions descriptor + Host `FilterWidgetRegistry` |
| Widget UI | Host `IFilterWidgetRenderer` (built-in + optional RCL plugin follow-up) |

Built-in widgets v1: `combobox`, `select`, `day`, `range`, `top`, **`chips`**.

Unknown widget → lint warning; runtime falls back to family default (`combobox` / `range`).

### 3. Action outcomes

`DashSpecActionOutcomeKind.RefreshCard` — handler returns card id; page controller re-renders single card.

### 4. Rejected

| Idea | Why |
|------|-----|
| Plugin-owned filter SQL | ADR-0033 |
| Plugin Blazor in Abstractions | keep Abstractions UI-free |
| Arbitrary chrome without block keyword | no discoverability |

## Consequences

- **Abstractions:** `ICardViewState`, `FilterWidgetContributorDescriptor`, `CardChromeContributorDescriptor`, `RefreshCard` outcome.
- **Core:** `CardViewSwitchApplier`.
- **Host:** `ExtensionChromeRenderer`, `FilterWidgetRegistry`, generic `CardExtensionChrome`.
- **Plugin:** `card_views` — block `views`, action `switch_view`.
