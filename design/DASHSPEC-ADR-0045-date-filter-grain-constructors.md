# DASHSPEC-ADR-0045: Date filter grain constructors

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-08-31 |
| **Relates to** | [ADR-0043](DASHSPEC-ADR-0043-filter-command-palette.md), [ADR-0044](DASHSPEC-ADR-0044-date-filter-value-constructor.md), [GUIDERS-ADR-0035](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0035-slash-value-constructors.md) |

## Context

[ADR-0044](DASHSPEC-ADR-0044-date-filter-value-constructor.md) ships day-level range construction (`date_range`). Users filter by **week**, **month**, and **quarter** grains — common in license analytics.

Static presets overlap with guided constructors. Grain constructors + free text replace enum presets in CCL.

Users also need **nested grains** — e.g. “2-я неделя месяца” vs “26-я неделя года (ISO)”. These are **virtual picker sub-modes**: separate `ArgConstructorBinding` rows that enter a shorter constructor tree, not flat enum presets.

## Decision

### 1. Wire grammar (`DateFilterPresets`)

| Token | Resolves to |
|-------|-------------|
| `today` | single day |
| `YYYY-Www` | ISO week Mon..Sun (`ISOWeek`) — **week of year** |
| `Www` | ISO week in **current year** |
| `YYYY-MM-Mn` | **n-th 7-day block of month** from day 1 (`n`=1..5) |
| `YYYY-MM` | full calendar month |
| `YYYY-Qn` / `Qn` | calendar quarter |
| `from..to` | explicit day range |

Month-week rule (v1): week 1 = days 1–7, week 2 = 8–14, … last week truncated to month end.

`last-week` / `last-month` remain for script compat only.

### 2. CCL arg entry — virtual picker sub-modes (constructors)

```text
argTail = picker+constructor:+date_today+date_week+date_month_week+date_month+date_quarter+date_range
```

| Virtual row | id | Sub-mode | Wire example |
|-------------|-----|----------|--------------|
| **Сегодня** | `date_today` | instant | `today` |
| **Неделя года…** | `date_week` | год → ISO-неделя | `2026-W26` |
| **Неделя месяца…** | `date_month_week` | год → месяц → n-я неделя | `2026-08-M2` |
| **Месяц…** | `date_month` | год → месяц | `2026-07` |
| **Квартал…** | `date_quarter` | год → Q1..Q4 | `2026-Q1` |
| **Период…** | `date_range` | day tree from..to | `2026-08-01..2026-09-15` |

**Pattern:** each row = `ArgConstructorBinding` (virtual picker entry) → dedicated composite/leaf catalog id. Platform unchanged ([GUIDERS-ADR-0035](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0035-slash-value-constructors.md) §6).

### 3. Leaf defs

| Leaf | Segments | Wire |
|------|----------|------|
| `week_grain` | year, week | `{year}-W{week}` |
| `month_week_grain` | year, month, month_week | `{year}-{month}-M{month_week}` |
| `month_grain` | year, month | `{year}-{month}` |
| `quarter_grain` | year, quarter | `{year}-{quarter}` |
| `date` | year, month, day | `{year}-{month}-{day}` |

### 4. Future virtual sub-modes (not v1)

Same binding pattern — product-only:

- декада месяца (`YYYY-MM-Dn`)
- половина месяца / полугодие
- rolling “последние N дней” as instant row

## Consequences

- `DateConstructorSegmentProvider`: `month_week` segment after year+month picked.
- `DashboardFilterCommandAcceptance`: instant `date_today`.
- Tests: month-week bounds, constructor emit → execute.
