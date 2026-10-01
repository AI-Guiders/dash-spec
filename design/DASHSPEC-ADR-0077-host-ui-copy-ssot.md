# DASHSPEC-ADR-0077: Host UI copy — SSOT and build-time freeze

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-02 |
| **Relates to** | [ADR-0076](DASHSPEC-ADR-0076-architecture-build-guards.md) |

## Context

User-facing strings were duplicated as **inline Cyrillic** in Blazor (`Components/Dashboard`, `Components/Pages`) while `DashboardLocalizer` already holds ru/en maps keyed by **English** phrases. Agents and PRs often “fix” UX by editing razor literals — shortening, rewording, or mixing languages without review.

## Decision

### SSOT for shell chrome copy

| Layer | Rule |
|-------|------|
| **Keys** | English phrase (stable API), e.g. `Localizer.T("Apply")` |
| **Translations** | `Services/Localization/DashboardLocalizer.cs` — `_en` + `_ruMap` |
| **Razor** | No new user-facing literals in `Components/Dashboard`, `Components/Pages`, `Components/Layout` |
| **Spec-driven labels** | Titles/labels from the loaded document stay in spec data, not hardcoded in chrome |

Long-form docs (e.g. `Help.razor`) are **legacy debt** tracked in the baseline until migrated to markdown + localizer or resource files.

### Enforcement (CI / `dotnet test`)

1. **Source guard** `ARCH-0077-CYRILLIC-INLINE` in `architecture/host-source-guards.json`  
   Cyrillic in scoped `.razor` files is forbidden **unless** the file is on the scope allowlist (legacy files only). **New** components cannot add inline Cyrillic at all.

2. **Baseline** `architecture/host-ui-cyrillic-baseline.json`  
   Per-file count of lines containing Cyrillic. Build fails if:
   - a file has Cyrillic but is **not** in the baseline (new inline copy);
   - count **increases** (changed copy without approval);
   - count **decreases** but baseline was not updated in the same PR (forces explicit migration commits).

Shrinking the baseline is the intended path when moving strings into `DashboardLocalizer`.

### Agent / operator process

- **Do not** paraphrase, abbreviate, or “improve” UI copy in code without the operator.
- **Do** add English key + both locales in `DashboardLocalizer`, then wire `Localizer.T(...)` in razor.
- To change wording or add inline text: **stop and ask the operator**; only after agreement, update localizer and baseline in one PR.

Cursor rule: `.cursor/rules/host-ui-copy-governance.mdc` (this repo).

## Non-goals

- `.resx` migration (optional later).
- Banning English literals in razor (baseline + localizer convention; English keys in `T("...")` are fine).

## Consequences

- Copy regressions and silent rewrites **fail CI** instead of shipping.
- Allowlist and baseline entries must **shrink** over time; growing baseline is a deliberate, reviewed act.
