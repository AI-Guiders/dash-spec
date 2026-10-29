---
type: ADR
id: DASHSPEC-ADR-0046
title: "CCL locale typed value input (DashSpec adapter)"
status: "Accepted"
date: 2026-08-31
tags: [dashspec, adr]
language: ru
---

# DASHSPEC-ADR-0046: CCL locale typed value input (DashSpec adapter)

## Decision

DashSpec Host adapts GUIDERS-ADR-0037:

- `IDashboardCultureAmbient` — request culture, not hardcoded ru-RU.
- `DashboardSlashConstructorHost` wires `SlashCompletionOptions` (registry + segment provider + culture).
- CCL: Tab completes path; locale date stream after path; constructor session survives arg-tail typing.
- `DateConstructorSegmentProvider` uses ambient culture for month labels.
- `ReadyWire` from platform guidance commits on Enter without wire memorization.

Wire SSOT remains `DateFilterPresets` at execute time.
