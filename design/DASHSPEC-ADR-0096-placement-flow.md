# DASHSPEC-ADR-0096: Placement flow (filter surfaces)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-07 |
| **Relates to** | [ADR-0093](DASHSPEC-ADR-0093-qualified-flow-graph-kinds.md), [ADR-0095](DASHSPEC-ADR-0095-dashboard-shell-wiring-pipeline.md) |

## Decision

- **`placement flow`** — dedicated qualified block for **where filter widgets appear** (Studio layer separate from `wire` / `data`).
- Short anchors: `report`, `page.<id>`, `card.<id>` (not `chrome.*`).
- **`show flow`** — legacy chrome/toolbar syntax and non-placement routes; new specs use `placement` for filters.
- Lowering: same runtime fields as toolbar routes (`DashboardFilters`, `LocalFilters`, page toolbar board).

## Example

```text
placement flow
  usage_date -> report
  app_name -> card.events_detail
end placement flow
```

## Pipeline

`DocumentCompilePipeline` → `DashboardShellWiringPipeline`: **data → placement → route (show/wire/action)**.
