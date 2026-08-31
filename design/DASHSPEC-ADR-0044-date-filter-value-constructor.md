# DASHSPEC-ADR-0044: Date filter value constructor (CCL)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-08-31 |
| **Relates to** | [ADR-0043](DASHSPEC-ADR-0043-filter-command-palette.md), [GUIDERS-ADR-0035](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0035-slash-value-constructors.md), [GUIDERS-ADR-0012](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0012-arg-picker-completion.md) |

## Context

[DASHSPEC-ADR-0043](DASHSPEC-ADR-0043-filter-command-palette.md) §3 defines date command wire grammar and Host preset table (`DateFilterPresets`). CCL date filters use `picker:enum:date_preset` — only `today`, `last-week`, `last-month` appear in the dropdown.

Users must remember wire formats (`YYYY-MM`, `2026-08-01..2026-08-31`) to set arbitrary ranges from the command line. Toolbar date widgets already provide calendar UX; CCL should offer **guided assembly**, not format documentation in hints.

[GUIDERS-ADR-0035](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0035-slash-value-constructors.md) defines platform **value constructors**. DashSpec is the first product adapter.

## Decision

### 1. Composite arg tail on date filter descriptors

Replace:

```text
argTail = picker:enum:date_preset
```

With:

```text
argTail = picker+constructor:date_preset+date_range
```

| Entry | Kind | Effect |
|-------|------|--------|
| `today`, `last-week`, `last-month` | preset | immediate wire token (unchanged) |
| `date_single` (optional) | constructor | single day → `YYYY-MM-DD` |
| `date_range` | constructor | period → `YYYY-MM-DD..YYYY-MM-DD` |

Virtual constructor rows appear in the same suggestion table as presets ([ADR-0043](DASHSPEC-ADR-0043-filter-command-palette.md) CCL UX).

### 2. Display vs wire (Russian locale default)

| Phase | User sees | Wire (ArgTail) |
|-------|-----------|----------------|
| Range start | `31.08.2026` | `2026-08-31` |
| Separator | ` .. ` | `..` |
| Range end | `15.09.2026` | `2026-09-15` |
| **Ready** | breadcrumb | `2026-08-31..2026-09-15` |

Display format: **`dd.MM.yyyy`** (Host chrome default; overridable later via spec chrome block — non-goal v1).

Wire format: **`yyyy-MM-dd..yyyy-MM-dd`** — matches existing `DateFilterPresets.TryResolve` and `DateDefaultRange`.

**Rule:** constructor MUST NOT add a second parser. Emitted wire MUST pass `SelectDateFilterCommand` unchanged.

### 3. Constructor hierarchy (not flat steps)

Per [GUIDERS-ADR-0035](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0035-slash-value-constructors.md) §0–§4 — **tree of reusable constructors**:

```text
Arg entry
├── Free text                        ← always available
├── today / last-week / …            ← presets
└── Range                            ← root composite
    ├── Date (from)                  ← child composite → leaf `date`
    │   ├── Year
    │   ├── Month
    │   └── Day
    ├── ..                           ← separator (auto)
    └── Date (to)                    ← same `date` leaf reused
        ├── Year
        ├── Month
        └── Day
```

DashSpec registers two catalog nodes:

| Id | Shape | Role |
|----|-------|------|
| `date` | leaf | segments: year → month → day; wire `yyyy-MM-dd` |
| `date_range` | composite | slots: `from`→`date`, `to`→`date`; wire `{from}..{to}` |

`date_single` (optional) = root slot `[date]` only — same leaf, no range wrapper.

#### Grain-aware variants (v1.1)

Derive truncated **leaf** defs from `date` — do not duplicate range tree:

| Grain | Leaf id | Segments | Wire |
|-------|---------|----------|------|
| `month` | `date_month` | year → month | `YYYY-MM` |
| `year` | `date_year` | year | `YYYY` |

v1 ships `date` + `date_range` on all toolbar date filters.

### 4. DashSpec implementation map

| Component | Role |
|-----------|------|
| `DateConstructorCatalog` | bundled `date` leaf + `date_range` composite defs |
| `DateConstructorNavigator` | DashSpec calendar rules (valid days, year window) — optional custom navigator |
| `DashboardCommandCatalogBuilder` | composite arg tail + constructor descriptor block |
| `DateFilterPresets` | **unchanged** — SSOT wire parse at Execute |
| `SelectDateFilterCommand` | **unchanged** |
| `DashboardFilterSlashCompletion` | delegate constructor phase to platform session when active |
| `DashboardCommandSession` | host scoped session; constructor draft isolated from page tree |

### 5. CCL UX rules

- Arg entry shows presets + **Период…** (Range) + free-text path (hint / continue typing)
- Accept preset → runnable immediately (Enter executes)
- Accept Range → enter tree at `from` → `date` leaf; breadcrumb reflects depth
- Breadcrumb shows human segments: `select › filter › Дата › 31.08.2026 .. 15.09.`
- Escape: cancel constructor → return to picker phase
- Backspace at step boundary: platform step back (W2); v1 may reset constructor

Highlight targets ([CommandSession](DASHSPEC-ADR-0043-filter-command-palette.md)) follow filter id during construction — no card re-render.

## Non-goals

- Calendar popover widget in CCL (constructor stays list-driven steps)
- Changing toolbar date widget behaviour
- New date grammar in `.dashspec`
- MCP/agent driving constructor steps (agents emit wire tokens)

## Consequences

- Depends on **CommandPlane.Slash** GUIDERS-ADR-0035 W1+ (types + session)
- DashSpec W2: implement constructors after platform lift; optional local spike beforehand
- Tests: constructor emit vectors + existing `DateFilterPresets` / executor tests stay valid

## Quarry wave

| Wave | Scope |
|------|-------|
| **W1** | Platform GUIDERS-ADR-0035 W1 in guiders-platform |
| **W2** | `DateRangeValueConstructor` + catalog composite tail + CCL integration |
| **W2a** | Tests: step emit → `TryExecute` → `FilterState` |
| **W3** | Grain-aware `date_month` / `date_year` constructors |

## Anti-patterns

- DashSpec-only constructor peel bypassing platform ADR-0035
- Teaching `YYYY-MM-DD` in `ArgHint` instead of constructor row
- Re-parsing display buffer in `SelectDateFilterCommand` — wire only
