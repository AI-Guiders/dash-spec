# DASHSPEC-ADR-0090: Report culture as locale SSOT

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-06 |
| **Relates to** | [ADR-0069](DASHSPEC-ADR-0069-report-time-basis-and-work-calendar.md), [ADR-0012](DASHSPEC-ADR-0012-host-presentation-layering.md) |

## Context

Date/time labels, number grouping, calendar week alignment, and filter slash parsing must not assume a single product locale (e.g. Russian `dd.MM`). BCL `CultureInfo` already encodes short/long date & time patterns, first day of week, and number formats.

## Decision

### `defaults.culture`

Report block may set:

```text
defaults {
  culture = "en-GB"
}
```

Value is a **specific BCL culture name** (`ru-RU`, `en-US`, `en-GB`, …). Neutral shorthand (`ru`, `en`) is rejected (`DashSpecCultures.Parse`).

Host bootstrap `[presentation].language` and `@host` `configuration.language` use the same rule. Cold-start default in code/TOML model: `ru-RU` (`DashSpecCultures.BootstrapDefaultName`).

### Precedence (presentation locale)

1. `defaults.culture` on the loaded dashboard document.
2. Host UI culture (`[presentation].language` / request localization).
3. `CultureInfo.CurrentCulture` only when neither report nor host culture is available (e.g. unit tests without ambient).

Runtime entry point: `LabelFormat.ResolveReportCulture()` / `FormatCulturePresets.ResolveCulture`.

### Named format presets

`date.short`, `date.full`, `time.short`, `datetime.short`, `date.iso`, `datetime.iso` resolve **patterns from the effective culture** (not hardcoded Russian templates). `system` uses host UI culture for general (`G` / `d`) formatting.

### Optional `date_format` / `time_format` / `datetime_format`

Remain **escape hatches** for ISO wire formats or explicit corporate patterns. Authors who only set `culture` should omit them.

### Calendar week (presentation)

Gantt weekly ticks and similar UI align to `culture.DateTimeFormat.FirstDayOfWeek` (`DashSpecCultures.StartOfCalendarWeek`). This is **display calendar**, not `work_days` from ADR-0069.

### Out of scope

- Per-user culture override in session toolbar (future).
- ICU-style weekend/holiday sets beyond BCL + ADR-0069 work calendar.
- Replacing wire/storage parsers (`DateValueCodec`) — data plane may add `parse_date` in dashflow separately.

## Consequences

- Host accepts full culture names in settings; product is not tied to ru-RU display rules.
- Tests should set `defaults.culture` or `LabelFormat.SetReportDefaults` when asserting locale-sensitive output.
