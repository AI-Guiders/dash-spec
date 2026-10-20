# DASHSPEC-ADR-0105: Filters pipe-table — `filters { | id | bind | … | }`

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-20 |
| **Relates to** | [ADR-0104](DASHSPEC-ADR-0104-table-columns-pipe-table.md) (pipe-table grammar family), [ADR-0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md), [ADR-0071](DASHSPEC-ADR-0071-block-and-member-grammar.md) |

## Context

Filter declarations were the last wide nested form: `filter <id>` + `bind {…}` + `show {…}` — three blocks per filter, one filter at a time. ADR-0104 made the pipe-table the canonical shape for attribute maps (columns). Filters are an attribute map.

## Decision

### Canonical syntax (inside `filters … end filters`)

```text
filters
  | id            | bind  | column                                                  | label    | widget   | ref           | range |
  | app_name      | field | lus.v_daily_active_users.app_name                      | Продукты | combobox | app_name      | -    |
  | activity_slot | date  | bucket_start_utc                                        | День     | day      | activity_slot | today|
  | activity_hour | field | lus.v_five_minute_activity_at_bucket.bucket_hour_hhmm | Час      | combobox  | activity_hour | -    |
end filters
```

- Header row declares the attributes present; subset headers valid; `-`/empty = unset; separator rows skipped (ADR-0104 semantics).
- Vocabulary: `id` (required, unique), `bind` (date/field/top, required), `column`, `label`, `widget`, `ref`, `single` (true/false), `grain`, `min`, `max`, `bind_scope`, `range`, `limit`.
- **Filter values ride the platform's own words**: `range` (date) and `limit` (top) — the same words the bind block and defaults blocks use. The word `default` stays rejected by design (ADR: "Filter value must be declared in defaults block"); the table columns use the platform vocabulary instead. Values resolve scope-defaults-first (same order as the nested form).
- **One semantics path**: rows go through the shared `FilterParser.validateSemantics` — date range normalization (`today` → `today..today` for day widgets), widget vocabulary, top min/max/limit rules. No parallel validation.
- The nested `filter <id>` block form stays valid (legacy during migration, file-by-file); both forms coexist in one `filters` container.
- `defaults`/`default` children stay (scope defaults inside the container); dedup rules unchanged.

### Mechanics

- `BlockGrammar` gains `TableRowRows` — bare pipe rows at container level (reusable; the same member serves future tables).
- `parseFiltersBlock` consolidates onto `BlockGrammar.parseKeywordContainer` (was a hand-rolled loop).
- Rows desugar directly into `FilterDefinition` records — no intermediate representation; the runtime is untouched.

## Consequences

- `filters` blocks parse via one container; future pipe tables (the `when` family) reuse `TableRowRows`.
- Field options, placement, toolbar wiring — all unchanged downstream (the IR is identical).
- MANUAL/authoring docs: the table form is the recommended shape; nested `filter` blocks are legacy.
