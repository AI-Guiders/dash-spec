# DASHSPEC-ADR-0069: Report time basis (calendar vs working) and work calendar v1

| Status | Proposed |
|--------|----------|
| Date | 2026-09-30 |

## Context

License-usage and operations dashboards often need the same **period grain** (day/week/month/year) but different **semantics**: wall-clock calendar time vs **working hours** inside a simple org calendar. Consumers (e.g. URSA dashspecs) declare defaults; the host may override org-wide settings.

## Decision

### `time_basis`

- `calendar` (default) — existing behaviour; date filters apply to the bound datetime/date columns only.
- `working` — queries additionally **clip** rows to configured work windows (v1: **clip** only in SQL; `measure` for utilization denominators is reserved).

### `time_apply` (v1)

- `clip` — append work-window predicates in `QueryCompiler` (implemented).
- `measure` / `both` — parsed and stored; SQL measure path deferred.

### Work calendar v1

Inline in spec `configuration` and/or host `report_time` settings (WitDB section):

| Key | Meaning |
|-----|---------|
| `work_timezone` | IANA or Windows TZ id for local work window |
| `work_start` / `work_end` | `HH:mm` local inclusive start, exclusive end |
| `work_days` | `mon,tue,wed,thu,fri` (ISO weekday names) |
| `work_time_column` | SQL column/expression (UTC `datetime2`) evaluated for work clipping |

**Out of scope v1:** holidays, transfers, per-user calendars, session toolbar override (future `К|Р`).

### Precedence

1. Host `report_time` settings (and optional TOML `[report_time]`) overlay spec `configuration`.
2. Spec `configuration` provides dashboard defaults.
3. Session override — not in v1.

### SQL (TSQL)

When effective basis is `working` and `time_apply` is `clip` or `both`, `QueryCompiler` ANDs a predicate on `work_time_column` using `AT TIME ZONE 'UTC' AT TIME ZONE '<windows id>'`, weekday mask, and local time range. Other dialects: no-op until implemented.

## Consequences

- Product feature lives in **dash-spec**; consumer repos only set `configuration` / host settings.
- Authors must set `work_time_column` when enabling `time_basis = working`.
- Month/year aggregates still use calendar period boundaries from date filters; working mode filters **events** inside those boundaries.
