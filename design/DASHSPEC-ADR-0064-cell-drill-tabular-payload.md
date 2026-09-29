# DASHSPEC-ADR-0064: Cell drill via table + tabular payload sources

| | |
|---|---|
| **Status** | Accepted (pilot track); structured carrier **Proposed** |
| **Date** | 2026-09-29 |
| **Relates to** | [ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md), [ADR-0029](DASHSPEC-ADR-0029-inspect-tooltip-presentation-split.md), [ADR-0030](DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md), [ADR-0012](DASHSPEC-ADR-0012-host-presentation-layering.md), [ADR-0018](DASHSPEC-ADR-0018-sql-datasource-carriers.md), [ADR-0063](DASHSPEC-ADR-0063-layout-board-nest.md) |

## Context

Heatmap drill-down today often uses:

1. **SQL** — `STRING_AGG` columns (`peak_users_by_host`, `activity_users_by_host`) on the heatmap row.
2. **Tooltip entity** — `@tooltip` renders a string for hover ([ADR-0029](DASHSPEC-ADR-0029-inspect-tooltip-presentation-split.md)).
3. **Host** — `on click { show below as list from tooltip }` parses that string (`split`, newline → «пик в …»), builds Russian headlines (`CardSelectionPresenter`), and renders `<ul>` outside the viz plugin stack.

Example (overview peak / activity):

```text
AutoCAD · 08:00 · 17 однов. · пик в 05:55

CAD-GPU060: marat.ismatov
…
```

This duplicates what **table** diagrams already do (detail tab, stakeholder phases) and keeps product semantics in the Host instead of spec + Execution.

Separately, authors asked whether drill/detail could be fed from **prepared structured data** (JSON, CSV, etc.) so the Host only decodes and hands rows to a diagram — same as SQL, without a second presentation path.

## Decision

### 1. Canonical drill UX = table diagram + selection state

For «список пар host:user (и метаданные ячейки)» the **canonical** v2 pattern:

| Layer | Responsibility |
|-------|----------------|
| **Spec** | `phase browse` (heatmap) + `phase detail` (table), or `nest` child table card ([ADR-0063](DASHSPEC-ADR-0063-layout-board-nest.md)) |
| **Click** | `on click { set <filter> from x\|y; focus detail }` ([ADR-0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md)) — no domain headlines in Host |
| **SQL / carrier** | Row-oriented view or payload: `host_name`, `user_sam`, optional `peak_bucket_hhmm` |
| **Title** | Card `title` / display bindings ([ADR-0058](DASHSPEC-ADR-0058-display-bindings.md)) — not `CardSelectionPresenter` |
| **Hover** | Keep `@tooltip` for compact inspect; may slim SQL once table drill exists |

**Pilot (LUS):** `peak_concurrent_proxy` and `activity_*` heatmaps — add drill views + phase/table; remove `show below as list from tooltip` on those cards.

**Deprecation (soft):** `show below as list|plain|kv from tooltip` remains for copy-only strips; new LUS cards should not add Host-formatted list drill.

### 2. Execution SSOT: tabular rows, not strings

Execution already normalizes query results to `IReadOnlyList<IReadOnlyDictionary<string, object?>>` before `ChartDataBuilder` / `TablePayloadBuilder`.

**Rule:** diagram families consume **tabular rows**. String tooltips are a **hover transport**, not a drill database.

Follow-up (Execution): optional `CellDrillContext` on click with **wire keys** (UTC bucket, `DateOnly`, raw dimension ids), not only display `XLabel`/`YLabel` — required when `set filter from x` must not use formatted `date.short` / `time.short` labels.

### 3. Structured sources (extension) — Proposed v1

Introduce a **tabular carrier** parallel to SQL view/file ([ADR-0018](DASHSPEC-ADR-0018-sql-datasource-carriers.md)):

| Carrier | Authoring (sketch) | Host / connector |
|---------|-------------------|------------------|
| **sql view / query** | today | connector SQL → rows |
| **file csv / json** | `datasource file "drill/peaks.json" format json` | read + schema map → rows |
| **inline column** | parent row column `drill_json` | parse JSON array → rows for child card (selection-scoped) |
| **static inline** | rare demos / tests | embedded in spec or resource |

**Host duty:** decode → validate column names → pass rows to the **same** `BuildTable` / viz path. **No** CSV/JSON-specific UI in Host — only connector + Execution.

**Non-goals v1:**

- Arbitrary nested JSON → multiple diagrams without schema.
- Host interpreting LUS-specific `host:user` text formats.

JSON shape (illustrative, not final grammar):

```json
[
  { "host_name": "CAD-GPU060", "user_sam": "marat.ismatov" }
]
```

Column names must match `table` diagram bindings.

### 4. Layering ([ADR-0012](DASHSPEC-ADR-0012-host-presentation-layering.md))

```text
Spec (phase, table columns, bind filters)
  → Execution (compile bind + selection → query or payload resolve)
  → Connector / payload decoder (rows)
  → TablePayloadBuilder
  → Viz plugin (table)
```

Remove from Host over time: `CardSelectionPresenter` headline/split logic for migrated cards; keep thin `CardSelectionDetail` only until list-from-tooltip is retired.

## Consequences

- **LUS:** new SQL views (e.g. cell drill by `usage_date` + `app_name` + optional bucket); overview spec phases; heatmap views may drop `STRING_AGG` drill columns when unused.
- **dash-spec:** parser/lint for `datasource file` + `format` — after pilot; connector interface `ITabularSource` or extend existing SQL connector with file backend.
- **Tests:** click → filter state → table row count; golden table payload from JSON fixture.

## Pilot checklist

- [ ] ADR linked from LUS `DATASOURCES_WARM_RU.md` drill section
- [ ] `lus.v_*_cell_drill` (or equivalent) row views
- [ ] `peak_concurrent_proxy` — `phase detail` + table card
- [ ] `activity_*` — same pattern (+ `activity_slot` / bucket wire keys)
- [ ] Host: document `XLabel` limitation; add wire keys if filters fail on pilot
- [ ] Optional: JSON file carrier spike behind feature flag
