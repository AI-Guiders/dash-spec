---
type: ADR
id: DASHSPEC-ADR-0077
title: "Host UI copy — SSOT and build-time freeze"
status: "Accepted"
date: 2026-10-02
tags: [dashspec, adr]
language: ru
relates:
  - DASHSPEC-ADR-0076
  - DASHSPEC-ADR-0090
---

# DASHSPEC-ADR-0077: Host UI copy — SSOT and build-time freeze

## Context

User-facing strings were duplicated as **inline Cyrillic** in Blazor (`Components/Dashboard`, `Components/Pages`) while `DashboardLocalizer` already holds ru/en maps keyed by **English** phrases. Agents and PRs often “fix” UX by editing razor literals — shortening, rewording, or mixing languages without review.

## Decision

### SSOT for shell chrome copy

| Layer | Rule |
|-------|------|
| **Keys** | English phrase (stable API), e.g. `Localizer.T("Apply")` |
| **Translations** | `Resources/HostStrings.resx` (neutral = English) + `Resources/HostStrings.ru.resx` — resource base name = `HostStrings` marker type full name |
| **Razor** | No new user-facing literals in `Components/Dashboard`, `Components/Pages`, `Components/Layout` |
| **Spec-driven labels** | Titles/labels from the loaded document stay in spec data, not hardcoded in chrome |

Long-form docs (e.g. `Help.razor`) are **legacy debt** tracked in the baseline until migrated to markdown + localizer or resource files.

### Enforcement (CI / `dotnet test`)

1. **Source guard** `ARCH-0077-CYRILLIC-INLINE` in `architecture/host-source-guards.json`  
   Cyrillic in scoped `.razor` files is forbidden **unless** the file is on the scope allowlist (legacy files only). **New** components cannot add inline Cyrillic at all.

2. **Baseline** `architecture/host-ui-cyrillic-baseline.json`  
   Per-file count of lines containing Cyrillic. Build fails if:
   - a file has Cyrillic but is **not** in the baseline (new inline copy);
   - count **increases** (changed copy without approval);
   - count **decreases** but baseline was not updated in the same PR (forces explicit migration commits).

Shrinking the baseline is the intended path when moving strings into `DashboardLocalizer`.

### Agent / operator process

- **Do not** paraphrase, abbreviate, or “improve” UI copy in code without the operator.
- **Do** add the English key + both locales to `HostStrings.resx` / `HostStrings.ru.resx`, then wire `Localizer.T(...)` in razor.
- To change wording or add inline text: **stop and ask the operator**; only after agreement, update localizer and baseline in one PR.

Cursor rule: `.cursor/rules/host-ui-copy-governance.mdc` (this repo).

### Plugins are vertical slices

A plugin carries everything it needs, including its UI copy.

| | |
|---|---|
| **Resources** | `<plugin>/Resources/<Plugin>Strings.resx` (neutral = English) + `<Plugin>Strings.ru.resx`; marker type name = resource base name |
| **Wiring** | Plugin Razor libraries add `<FrameworkReference Include="Microsoft.AspNetCore.App" />` and inject `IStringLocalizer<PluginStrings>` (host `AddLocalization` supplies the factory) |
| **Neutral locale** | English; Russian is a literary translation authored in the plugin, not machine-translated |
| **Host contract** | No host-side `IVizCardToolbarLocalizer`-style indirection: plugin copy never sits in `HostStrings` |

Scope — UI copy vs plugin metadata: this rule covers user-facing copy in Razor components. Plugin **metadata** (`DisplayName`, action descriptions such as `"Export card data as CSV"`) stays English by convention in *all* plugins (the viz plugin too) and is not localized; visible captions (e.g. `Export CSV`) come from the host resx.

Current state: the staged plugins (`Export`, `ViewSwitch`, connectors) ship **no Razor and no localized copy**, so they carry no resources; connectors have no UI at all. Plugins referenced directly (viz, filter) ship their satellites automatically. If a staged plugin ever gains a resx, `StageConnectorPlugins` must copy its satellites too — it copies only the main DLL today.

### Localization mechanics (Blazor Server)

- `DashboardLocalizer` wraps `IStringLocalizer<HostStrings>`; `T(key)` is the stable API, `T(key, args…)` formats resx placeholders (`{0}`…).
- Culture: `AddLocalization` + `RequestLocalizationOptions` (supported ru-RU/en-US/en-GB) + `UseRequestLocalization`. Blazor Server circuits render on pool threads, so host bootstrap also sets `CultureInfo.DefaultThreadCurrentUICulture`.
- Host language (`[presentation].language`, ADR-0090) is authoritative: `RequestCultureProviders` is cleared so a browser `Accept-Language` cannot override it.
- Plugins resolve their own resources (`IStringLocalizer<PluginStrings>`); the host-side `IVizCardToolbarLocalizer` indirection was removed.

## Non-goals

- Banning English literals in razor (baseline + localizer convention; English keys in `T("...")` are fine).

## Consequences

- Copy regressions and silent rewrites **fail CI** instead of shipping.
- Allowlist and baseline entries must **shrink** over time; growing baseline is a deliberate, reviewed act.
