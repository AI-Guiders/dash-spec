# DASHSPEC-ADR-0043: Filter command palette (CommandPlane adapter)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-08-28 |
| **Relates to** | [ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md), [ADR-0010](DASHSPEC-ADR-0010-spec-ergonomics.md), [ADR-0022](DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md), [ADR-0037](DASHSPEC-ADR-0037-filter-scopes-and-toolbar-grouping.md), [ADR-0042](DASHSPEC-ADR-0042-host-control-center-witdb.md), [GUIDERS-ADR-0009](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0009-command-surface-pattern.md), [GUIDERS-ADR-0010](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0010-platform-mechanics.md), [FORGE-ADR-0048](https://github.com/AI-Guiders/agent-forge/blob/main/design/FORGE-ADR-0048-human-primary-surface-and-command-palette.md), [FORGE-ADR-0025](https://github.com/AI-Guiders/agent-forge/blob/main/design/FORGE-ADR-0025-human-command-parity.md) |

## Context

Toolbar filters (`filter`, `toolbar`, chips/date widgets) — единственный способ сменить срез данных в Host. Для power-user и agent parity нужен **slash / command palette** (`/select …`), как в Forge/CIDE — но **без второго языка фильтров** в `.dashspec`.

В Guiders stack уже есть платформенный слой:

- **Catalog** — discoverability (`SlashCommandDescriptor`, path, help, arg_tail)
- **Registry** — `PlatformCommandRegistry<TContext>` → `TryExecute(commandId, …)`
- **Command** — `IPlatformCommand<TContext>` / `PlatformCommand<TContext>`
- **Surface** — slash bar, `Ctrl+K`, toolbar, MCP — параллельные invokers одного executor

DashSpec Host должен быть **product adapter** к `AIGuiders.Platform.CommandPlane`, не собственный slash-движок.

## Decision

### 1. Один state, много surfaces

```text
Spec:   filter app_name … widget = chips
        toolbar usage_date, app_name

Host:   FilterState (SSOT на report session)
          ↑                    ↑
    toolbar widgets      /select app AutoCAD
    (Surface)            (Surface → Registry → Command)
```

Команды **мутируют тот же `FilterState`**, что и chips/date picker. После `Execute` — тот же refresh pipeline (`apply = auto` / manual apply на card).

### 2. CommandPlane wiring (DashSpec Host)

| Pattern | DashSpec |
|---------|----------|
| **Context** | `DashboardFilterContext : ICommandContext` — report id, resolved toolbar filter defs, current `FilterState`, connector for picker distincts |
| **Registry** | `DashboardCommandRegistry` (= `PlatformCommandRegistry<DashboardFilterContext>`) |
| **Catalog** | bundled descriptors `select/date`, `select/<alias>`; merge plugin contributions |
| **Commands** | `SelectDateFilterCommand`, `SelectFieldFilterCommand`, … — one `Execute` each |

**Rule (GUIDERS-ADR-0009):** surface handlers (slash JS, palette Blazor, MCP) вызывают только `registry.TryExecute(commandId, context)` — без inline filter math.

### 3. Slash grammar (Host, not spec)

Prefix: **`/select`** (v1). Аргументы парсятся `SlashLineResolver` / ArgTail mechanics из CommandPlane.

#### Date filters

Target: первый **date** filter на toolbar report (или alias из optional `commands` block — §5).

| Invocation | Effect |
|------------|--------|
| `/select date today` | `today..today` |
| `/select date last-week` | preset → `-7d..today` |
| `/select date last-month` | preset → календарный предыдущий месяц `YYYY-MM-01..YYYY-MM-last` |
| `/select date 2026-07` | `2026-07-01..2026-07-31` |
| `/select date 2026-07-08..2026-07-15` | explicit range (same grammar as filter `default`) |

Presets — **Host table** (mechanic), не ключевые слова в `.dashspec`.

#### Field filters (app, user, …)

Target: field filter by **alias** or filter id (`app_name`, `user_sam`, …).

| Invocation | Cardinality | Effect |
|------------|-------------|--------|
| `/select app AutoCAD` | single | one value; fuzzy match; ambiguous → picker |
| `/select app all` | reset | clear restriction (all values allowed) |
| `/select app [AutoCAD, Revit, Civil 3D]` | multi | list; each token → picker if ambiguous |

**Widget hint:** `widget = combobox` → default single; `widget = chips` → default multi. Command may override cardinality for the invocation; state model stays one filter id.

Picker reuses the same distinct/options source as toolbar chips (connector + bound column).

### 4. Surfaces (v1)

| Surface | Notes |
|---------|-------|
| **Toolbar widgets** | existing; unchanged |
| **Command bar / slash** | typed `/select …`; autocomplete from catalog |
| **Command palette (`Ctrl+K`)** | same catalog; Forge-style affordance ([FORGE-ADR-0048](https://github.com/AI-Guiders/agent-forge/blob/main/design/FORGE-ADR-0048-human-primary-surface-and-command-palette.md)) |
| **MCP / agent** | optional v1.1: tool with same `commandId` ([FORGE-ADR-0025](https://github.com/AI-Guiders/agent-forge/blob/main/design/FORGE-ADR-0025-human-command-parity.md)) |

Surfaces **parallel** — palette не заменяет toolbar ([ADR-0042](DASHSPEC-ADR-0042-host-control-center-witdb.md) Control Center — другая ось: settings, не filter state).

### 5. Optional spec aliases (ergonomics only)

Core **не парсит** slash. Опциональный блок — только **alias → filter id**:

```text
commands
  date = usage_date
  app = app_name
  user = user_sam
end commands
```

Без блока: catalog использует filter id как path segment (`/select app_name …`).

### 6. Scopes

| Scope | Behaviour |
|-------|-----------|
| **Report toolbar** | default target for `/select` |
| **Card-local filters** (`filters { … }`, `apply = manual`) | v1: command sets **toolbar** filters only; card-local via future `/select @card …` (non-goal v1) |
| **Embedded tab** ([ADR-0011](DASHSPEC-ADR-0011-tab-modules.md)) | context = active report module |

### 7. URL / share

Filter state serializes to query string (existing Host behaviour). Commands write the same keys — `/select date last-week` ≡ user picked range in date widget.

## Non-goals v1

- Slash syntax in `.dashspec` body (no `@command` module)
- Second filter executor outside CommandPlane
- Replacing toolbar with palette-only UX
- Card-local `/select` without explicit scope token
- New filter types — only commands over existing `filter date` / `filter field` / `filter top`

## Consequences

- Host references **`AIGuiders.Platform.CommandPlane`** NuGet.
- New filter UX = new **command class** + catalog descriptor + tests — not new branches in Blazor toolbar code.
- Agent can drive the same dashboard slice as human via shared `commandId`.
- LUS/Forge deploy cadence independent — DashSpec picks up platform package version in Host csproj.

## Implementation waves

| Wave | Scope |
|------|-------|
| **W0** | ADR + `DashboardFilterContext` + registry host; no UI |
| **W1** | `SelectDateFilterCommand` + presets; slash bar on dashboard page |
| **W2** | `SelectFieldFilterCommand` (single / all / multi + picker) |
| **W3** | `Ctrl+K` palette + optional `commands { }` aliases in Core parser |
| **W4** | MCP tool / plugin `RegisterCommands` hook |

## Alternatives considered

| Alternative | Why not |
|-------------|---------|
| DashSpec-owned slash parser | Duplicates CommandPlane; breaks agent parity |
| Filter commands in spec DSL | Two sources of truth vs bind-only filters |
| Palette mutates SQL directly | Bypasses `FilterState` / bind compiler |
| Reuse Forge `POST /commands/execute` remotely | DashSpec Host is standalone product; same **pattern**, local registry |

## Related docs

- Filter bind model: [ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md), [FILTERS_RU.md](../docs/FILTERS_RU.md)
- Tooltip vs filter: [ADR-0029](DASHSPEC-ADR-0029-inspect-tooltip-presentation-split.md) — inspect = **what to show**; `/select` = **which slice**
