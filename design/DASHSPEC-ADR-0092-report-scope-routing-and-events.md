# DASHSPEC-ADR-0092: Report-scope routing and events

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-06 |
| **Relates to** | [ADR-0091](DASHSPEC-ADR-0091-unified-graph-flow-and-routing.md), [ADR-0090](DASHSPEC-ADR-0090-card-interior-flow.md), [ADR-0009](DASHSPEC-ADR-0009-bind-only-filters.md), [ADR-0073](DASHSPEC-ADR-0073-author-navigation-surface.md) |

## Context

[ADR-0091](DASHSPEC-ADR-0091-unified-graph-flow-and-routing.md) unifies **card** and **module** wiring as link lines. Report-level behaviour still uses parallel notations:

| Concern | Legacy authoring |
|---------|------------------|
| Dashboard toolbar | `toolbar` + `filter` defs |
| Card filter chrome | `filters { … }` on card |
| Browse → detail | `filters host <host_card>` |
| Derived filters | `derive usage_date from period_start …` |
| Drill / navigation | `on click` (`set`, `focus`, `goto`) |

Authors and Studio need the same **Graph IR** and `producer -> consumer` syntax at report scope.

## Decision

### Same wire shape, wider scope

Link lines use the grammar from [ADR-0091](DASHSPEC-ADR-0091-unified-graph-flow-and-routing.md). **No** `route … end route` container — only `flow … end flow` (name is historical; edges are classified by ports).

Containers (merge into one document **Graph** at compile time):

| Container | Typical edges |
|-----------|----------------|
| `report … flow` | Dashboard chrome, cross-card defaults |
| `page` / `tab` `flow` | Page-local host and navigation |
| `card … flow` | Module rows + slot filters ([ADR-0090](DASHSPEC-ADR-0090-card-interior-flow.md)) |

### Node kinds (report scope)

| Node | Role |
|------|------|
| `filter` | Definition block; participates as endpoint |
| `chrome` | Toolbar or card filter panel (`dashboard`, `card <id>`, optional `host <id>`) |
| `card` | Layout target; host browse cards export filter values |
| `slot` | Diagram slot on a card (for cross-card references in later lint) |
| `phase` / `page` | Navigation targets for drill |

Row-moving **flow** edges at report scope are rare (module graph stays in `.dashflow`). Report `flow` is mostly **route** and **event**.

### Edge classification

| Producer | Consumer | `edgeKind` |
|----------|----------|------------|
| `filter` id | `chrome dashboard` or `[toolbar]` chrome | **route** |
| `filter` id | `chrome card <card_id>` | **route** |
| Host `card` filter value (browse) | Child card chrome / slot port | **route** |
| `filter` id | `filter` id (derive) | **route** |
| Slot / diagram click source | `filter` id (`set` target) | **event** |
| Slot / diagram click source | `phase` / `page` (`focus`, `goto`) | **event** |

Port names on chrome: `[toolbar]`, `[panel]`, or filter id as `in` port when `chrome` declares `ports` (same pattern as slot ports in ADR-0091).

### Authoring examples (target)

Dashboard toolbar (replaces implicit `toolbar` list for **wiring**; layout/order may stay on `toolbar` until lowered):

```text
report
  filter date usage_date on …
  filter field app_name on …

  flow
    usage_date -> [toolbar] chrome dashboard
    app_name -> [toolbar] chrome dashboard
  end flow

  toolbar
    usage_date
    app_name
  end toolbar
```

Card-local filter panel:

```text
card activity_5min as "Activity 5-min" {
  filter date activity_slot on …

  flow
    activity_slot -> [panel] chrome card activity_5min
  end flow

  filters
    activity_slot
  end filters
  …
}
```

Host browse (detail cards consume host chrome):

```text
page analytics_drill {
  flow
    events_browse.user_name -> host chrome card events_detail
    events_browse.app_name -> host chrome card events_detail
  end flow

  card events_browse …
  card events_detail …
    filters host events_browse
  end card
}
```

Exact host port syntax (`browse_peak.user_name` vs named `host` node) is resolved at implementation; IR stores **host card id**, **filter id**, **consumer card id**.

Derive as filter-to-filter route (metadata on `filter` blocks stays for grain/column):

```text
flow
  period_start -> usage_date
end flow
```

`derive … from … grain …` lowers to the same edge plus edge attributes copied from the legacy block.

Click / drill (event edges; behaviour details stay in `on click` until lowered):

```text
card peak_concurrent_proxy {
  flow
    heatmap [click.app_name] -> [value] app_name
    heatmap [click] -> focus phase detail
  end flow

  on click
    set app_name from heatmap
    focus phase detail
  end click
}
```

### Definitions stay blocks

Unchanged as **blocks only** (not link lines):

- `filter …` (column, widget, label)
- `toolbar chrome …` (sticky, debounce, layout)
- `derive …` (until migrated to links)
- `on click` effect bodies (until migrated to event edges)
- `diagram`, `views`, `layout`, `limits`, `when`

### Legacy → links (migration)

| Legacy | Link form (report/page `flow`) |
|--------|--------------------------------|
| `toolbar` names only | `f -> [toolbar] chrome dashboard` per filter |
| `filters { f }` on card | `f -> [panel] chrome card <id>` |
| `filters host H` | host routes from card `H` to consumer card |
| `derive A from B …` | `B -> A` (+ attrs) |
| `on click set A from slot` | `slot [click…] -> [value] A` |
| `focus` / `goto entry` | event edge to `phase` / `page` node |

Card interior legacy (`bind`, `input from`) remains in [ADR-0090](DASHSPEC-ADR-0090-card-interior-flow.md).

### Compile / runtime

1. Parser accepts optional `flow` on `report`, `page`/`tab`, and `card`.
2. Lowering pass materializes legacy blocks into **route** / **event** edges when links are absent (backward compatible).
3. Studio renders one graph; filters by `edgeKind` and scope (`report:`, `page:`, `card:`).
4. Execution semantics unchanged until binders read Graph IR instead of parallel lists (phased).

## Phasing (within ADR-0091 P3)

| Step | Deliverable | Code (develop) |
|------|-------------|----------------|
| **P3a** | Parse `report` / `page` `flow`; dashboard + card chrome route edges; lower `toolbar` / `filters { }` | `ReportScopeFlowParser`, `DocumentWiringGraphBuilder` |
| **P3b** | Host routes; lower `filters host` | `ReportScopeFlowApplicator` (+ graph host edges from legacy) |
| **P3c** | Derive as filter→filter edges | `DocumentWiringGraphBuilder.lowerPageDerive` |
| **P3d** | Event edges; lower `on click` set/focus/goto | `DocumentWiringGraphBuilder.lowerCardClicks` (runtime still uses `on click`) |

`DashboardDocument.WiringGraph` is populated on parse. Explicit scope links may set `FilterHostCardId` when targeting `host.chrome.card.<id>`. Studio export and runtime binders reading Graph IR remain follow-up.

Depends on **P2** (`slot` / `ports`, Graph IR export) for lint across card boundaries.

## Non-goals

- Second wire syntax at report scope.
- Route edges that execute SQL or transform row batches.
- Replacing `toolbar chrome` presentation properties with links.
- Product-specific report ids in this ADR (use `samples/demo` names in examples only).

## Follow-up

- Tab embed filter inheritance ([ADR-0011](DASHSPEC-ADR-0011-tab-modules.md)): optional explicit route edges from parent tab instead of implicit soak only.
- Authoring guide section pointing here once P3a ships.
