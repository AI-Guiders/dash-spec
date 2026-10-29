---
type: ADR
id: DASHSPEC-ADR-0114
title: "Switch slot — multi-payload diagram slot"
status: "Accepted"
date: 2026-10-27
tags: [dashspec, adr]
language: ru
relates:
  - DASHSPEC-ADR-0064
  - DASHSPEC-ADR-0065
  - DASHSPEC-ADR-0107
  - DASHSPEC-ADR-0111
---

# DASHSPEC-ADR-0114: Switch slot — multi-payload diagram slot

## Context

A heatmap card with two drill tables needed **two interior slots** (`drill`, `app_drill`) because the
model allowed one diagram per slot. Side by side with the map, both cells sat empty most of the time,
each with its own awaiting hint (live pain: the `peak_concurrent_proxy` heatmap card, 2026-10-26). The
tables are not two independent facts — they are **one drill zone** whose content depends on what was
clicked (a cell → the at-peak table; an axis label → the users-by-app table).

An earlier design pass proposed an in-cell *replacement* of the diagram (`swap into`). Rejected: the
operator's sketch keeps **two cells visible at all times** — the map never goes anywhere; the second
cell is itself a **switch** between tables.

## Decision

### 1. One slot, N payloads

```text
diagram ref drill
  payload peak_table  lus_peak_concurrent_proxy_drill_table
  payload users_table lus_peak_concurrent_app_users_table
```

- A slot is **either** classic (`diagram ref <slot> <name>`) **or** a switch slot (payload lines) —
  never both (`CardDiagramSlotBuilder.addPayload` raises on the mix).
- The head payload feeds the classic `Diagram` field: kind resolution, awaiting state, and every
  consumer that does not know about payloads keep working unchanged.
- Payload diagram resolution: imported diagram or preset id only (no inline kind blocks).
- Slot-level wiring stays slot-level: routes (`filter -> [port] slot`) land in `BoundFilters` for
  every payload.

### 2. Addressing: `slot.payload`

```text
on click
  | source        | drill             | set                       |
  | cell          | drill.peak_table  | usage_date=x, app_name=y  |
  | category item | drill.users_table | app_name=y                |
end click

data flow
  at_peak_tz [rows] -> [rows] drill.peak_table
  users_by_app_tz [rows] -> [rows] drill.users_table
end data flow
```

- Click targets and module-flow links accept `slot.payload`; the slot part must be a declared slot,
  the payload part must be declared on it. A bare slot name on a switch slot is **ambiguous → parse
  error** (author must pick a payload).
- Report-scope flow (`card.<id>.<slot>`) keeps slot-level granularity in v1; dotted targets there
  fail with the existing loud "expected card.<cardId>.<slotRef>" error.

### 3. Runtime semantics

- **Active payload** rides the existing per-card click state (`CardCellDrillOverlay.TargetPayload`,
  beside `TargetSlot` from ADR-0107). Session state, plane immutable — ADR-0111 §4.
- **Last click wins**: clicking the map again replaces the active payload in the same cell; there is
  no per-payload chrome.
- **Awaiting**: a drill switch slot with no overlay (or a stale payload name) renders the awaiting
  hint without executing a query — same branch as a classic drill slot. With several sources filling
  one slot the hint is the combined text ("Click a heatmap cell or an axis label…", `AwaitingSource =
  "switch"`); the single-source case keeps the source-specific hint.
- **Coverage** (`CardInteriorLayoutCompactor`): unchanged — a switch slot is one slot with one
  placement; payloads never appear on the board.
- **Validation** (`ReportScopeDataFlowApplicator`): a switch slot is wired when every payload has a
  flow input (or the slot carries a real `DataSource`).

### 4. What was rejected along the way

| Rejected | Why |
|----------|-----|
| Board pipe `[ heatmap\|drill ]` (in-cell diagram⇄table swap) | Solved a different problem (space), replaced the map, needed a "back" affordance the sketch did not have |
| `swap into <slot>` on the slot | Same replacement semantics; conflated "where it lives" with "what it is" |
| `switch` modifier inside `on click` rows | Repeats the target per row; mixes "what to put" with "where it goes" in one table |
| Keeping two slots + shared board cell | Coverage/placer would need a new "two tokens, one cell" concept — a bigger grammar change than payloads |

## Consequences

- Parser: `CardDiagramSlot.Payloads` (`CardSlotPayload.Name/Diagram/FlowInput`), payload block parse,
  dotted-target validation, dotted flow links (`CardInteriorFlowResolver`).
- Runtime: `CardCellDrillOverlay.TargetPayload`; `CardDrillSlotTargets` collects **all** sources per
  slot (dotted targets fold into their slot); `CardRenderService` picks the active payload before
  building the slot card.
- Editor intelligence (completion / go-to-def for `slot.payload`) — not in this slice.
- Report-scope dotted flow targets — not in this slice.
- Pilot: the `peak_concurrent_proxy` card — `[ heatmap:3 drill:1 ]`, one switch slot, both tables in
  one cell; verified live (awaiting → `Запуски: AVEVA E3D` → cell click → `AspenTech · 21.10`).
