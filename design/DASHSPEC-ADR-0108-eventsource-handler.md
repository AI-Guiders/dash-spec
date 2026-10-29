---
type: ADR
id: DASHSPEC-ADR-0108
title: "EventSource + Handler — the interaction subsystem"
status: "Accepted (direction)"
date: 2026-10-20
tags: [dashspec, adr]
language: ru
relates:
  - DASHSPEC-ADR-0106
  - DASHSPEC-ADR-0107
  - DASHSPEC-ADR-0033
---

# DASHSPEC-ADR-0108: EventSource + Handler — the interaction subsystem

## Context

ADR-0107 made clicks source-explicit — and the cost curve exposed the smell: the
interaction logic is smeared across six places (the page controller's dispatch, the
resolver, a separate overlay state, the renderer's overlay gating, the refresh
coordinator's two paths, the refresh planner). Every new interaction feature pays
in all of them. And `HeatmapCellContext` with its fixed `x|y|value` bind words is a
cell-shape hardcoded into the platform.

The click family (matrix `cell`, `category item`; chart `category`) is one real event
kind. More kinds will come (hover, selection, tick) — each with its own context shape.

## Decision

### The model

- **EventSource** — plugin-declared (the renderer knows what it can emit):
  an id (plugin-local vocabulary, like filter widget ids) + a context schema
  (the fields its events carry). The platform does not know what a heatmap's axes are.
- **Handler** — spec-declared, via the model SSOT (ADR-0107 `ClickHandlers` generalizes
  to event handlers): `on <source-id> { effects }`. Effects stay platform vocabulary
  (`set` / `drill table` / `goto` / `focus` / `invoke` / `show`).
- **The interaction subsystem** (core, one service) owns the runtime SSOT:
  - dispatch: event(source, context) → source-specific handler → generic fallback;
  - the event channel: `OnSurfaceEvent(source, context)` replaces per-kind render callbacks;
  - the effect executor: set (session/card-local), drill (the overlay + slot targeting),
    goto/focus (navigation), invoke (plugin actions), show (selection display);
  - the refresh decision (interior slots vs full card, ADR-0066);
  - the render integration: the renderer asks the subsystem for the effective filters of
    a slot — the renderer does not know about overlays.

### Field binding generalization (waits for the second event kind)

`set <filter> from x|y|value` binds from the **source's declared context fields**
(`from y_label`). The fixed `x|y|value` vocabulary stays until a real source needs its
own fields (a table `row_click` with row fields) — the family rule: the form earns its
shape from real instances.

### Not events (checked, different axes)

`invoke`/action handlers are commands; phrase templates are sugar over `invoke`;
`when` blocks are visibility state.

### Entity boundary (one author → registry, not the spec language)

An `EventSource` is a real entity — a descriptor (`PluginId`, `SourceId`,
`ContextSchema`) registered by the plugin — not a spec-file module. The test is
authorship: diagrams/flows/filters are content (authored per deployment, so they
live in specs); events are properties of the renderer (one author — the plugin —
so they live in the registry, like widgets, diagram kinds, action handlers, and
phrase templates; the registry is the discovery). Overengineering, explicitly
rejected: a `module events` spec kind (zero new information), typed context
payloads before a source needs its own fields (waits for a table `row_click`),
an event bus (no consumers), or multiple handlers per event (one specific +
generic fallback is the proven shape).

## Consequences

- One registry (`EventSource`), one channel, one dispatch, one executor — the smear
  collapses into the subsystem.
- Plugins declare event sources exactly like they declare widgets and click sources
  today (ADR-0107 registry is the seed; the rename rides the refactor).
- Migration order: the sourced-clicks feature lands first (green baseline), then the
  subsystem refactor under this ADR — behavioral-neutral, gated by the test suite.
