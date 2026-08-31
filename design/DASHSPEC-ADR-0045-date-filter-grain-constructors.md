# DASHSPEC-ADR-0045: Date filter grain constructors

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-08-31 |
| **Relates to** | [ADR-0043](DASHSPEC-ADR-0043-filter-command-palette.md), [ADR-0044](DASHSPEC-ADR-0044-date-filter-value-constructor.md), [GUIDERS-ADR-0035](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0035-slash-value-constructors.md) |

## Context

[ADR-0044](DASHSPEC-ADR-0044-date-filter-value-constructor.md) ships day-level range construction (`date_range`). Users filter by **week**, **month**, and **quarter** grains — common in license analytics.

Static presets (`today`, `last-week`, `last-month`) overlap with guided constructors and clutter the arg menu. Grain constructors + free text are sufficient.

## Decision

### 1. Wire grammar (`DateFilterPresets`)

| Token | Resolves to |
|-------|-------------|
| `today` | single day (free text + instant constructor row) |
| `YYYY-Www` | ISO week Mon..Sun (`System.Globalization.ISOWeek`) |
| `Www` | same week in **current year** |
| `YYYY-MM` | first..last day of month |
| `YYYY-Qn` | first..last day of quarter (`n` = 1..4) |
| `Qn` | same quarter in **current year** |
| `from..to` | explicit day range (unchanged) |

`last-week` / `last-month` remain in `TryResolve` for backward-compatible scripts; **removed from CCL dropdown**.

### 2. CCL arg entry — constructors only (no enum presets)

```text
argTail = picker+constructor:+date_today+date_week+date_month+date_quarter+date_range
```

| Row | id | Steps | Wire |
|-----|-----|-------|------|
| **Сегодня** | `date_today` | instant (accept handler) | `today` |
| **Неделя…** | `date_week` | год → ISO-неделя | `2026-W26` |
| **Месяц…** | `date_month` | год → месяц | `2026-07` |
| **Квартал…** | `date_quarter` | год → Q1..Q4 | `2026-Q1` |
| **Период…** | `date_range` | from/to day tree | `2026-08-01..2026-09-15` |
| Free text | — | manual | any grammar above |

Each grain (except today) is a **single-slot composite** over a leaf — no platform changes.

### 3. Leaf defs (`DateConstructorCatalog`)

| Leaf | Segments | Wire pattern |
|------|----------|--------------|
| `week_grain` | year, week | `{year}-W{week}` (week `WireMinWidth: 2`) |
| `month_grain` | year, month | `{year}-{month}` |
| `quarter_grain` | year, quarter | `{year}-{quarter}` (`Q1`..`Q4`) |
| `date` | year, month, day | `{year}-{month}-{day}` (range only) |

### 4. Non-goals (v1)

- Year-only grain (`2026`)
- Decade navigation
- Fiscal-year quarter offset

## Consequences

- `DateConstructorSegmentProvider`: `week` segment (ISO weeks per year).
- `DashboardFilterCommandAcceptance`: instant `date_today` → `today` wire.
- Tests: ISO week bounds, all grain constructors → execute.
