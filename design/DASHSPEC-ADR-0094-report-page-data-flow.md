---
type: ADR
id: DASHSPEC-ADR-0094
title: "Report/page data flow"
status: "Accepted"
date: 2026-10-07
tags: [dashspec, adr]
language: ru
relates:
  - DASHSPEC-ADR-0093
  - DASHSPEC-ADR-0078
  - DASHSPEC-ADR-0090
---

# DASHSPEC-ADR-0094: Report/page data flow

## Decision

- **`source` / `transformer`** live only in module `@flow` (`.dashflow` via `connect`).
- **Report and page** may declare **`data flow`** with the same link grammar as cards.
- Data-flow consumers on report/page use **`card.<cardId>.<slotRef>`** (not bare slot names).
- **Card** may still use **`data flow`** with bare slot ids for filter→slot and optional local module→slot links; prefer report/page for module→card fan-out.
- **Removed from cards (parse error):** `data { }`, `data for`, `datasource`, `input` / `input for`.

## Examples

```text
report
  data flow
    peak_tz [rows] -> [rows] card.kpi.diagram
    usage_date -> [usage_date] card.kpi.diagram
  end data flow
end report
```

Module graph remains in `connect { flow "…" }`.
