# DASHSPEC-ADR-0046: CCL locale typed value input (DashSpec adapter)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-08-31 |
| **Relates to** | [GUIDERS-ADR-0037](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0037-slash-locale-typed-value-input.md), DASHSPEC-ADR-0043, DASHSPEC-ADR-0044, DASHSPEC-ADR-0045 |

## Decision

DashSpec Host adapts GUIDERS-ADR-0037:

- `IDashboardCultureAmbient` — request culture, not hardcoded ru-RU.
- `DashboardSlashConstructorHost` wires `SlashCompletionOptions` (registry + segment provider + culture).
- CCL: Tab completes path; locale date stream after path; constructor session survives arg-tail typing.
- `DateConstructorSegmentProvider` uses ambient culture for month labels.
- `ReadyWire` from platform guidance commits on Enter without wire memorization.

Wire SSOT remains `DateFilterPresets` at execute time.
