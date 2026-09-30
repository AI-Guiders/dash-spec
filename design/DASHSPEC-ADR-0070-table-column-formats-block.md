# DASHSPEC-ADR-0070: Table column formats — `formats` block

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-30 |
| **Relates to** | [ADR-0004](DASHSPEC-ADR-0004-diagram-column-as.md), [ADR-0064](DASHSPEC-ADR-0064-cell-drill-tabular-payload.md), [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) |

## Context

Drill tables need per-column display presets (`datetime.short`, `date.short`, …) — same vocabulary as `x_format` / `y_format` on heatmaps ([ADR-0004](DASHSPEC-ADR-0004-diagram-column-as.md)). Inline `column_formats = col:preset` is **not** supported (see [ADR-0071](DASHSPEC-ADR-0071-block-and-member-grammar.md)).

## Decision

### Canonical syntax (inside `table`)

```text
table
  columns = host_name, user_sam, bucket_start_utc
  formats
    bucket_start_utc = datetime.short
    peak_bucket_start_utc = datetime.short
  end formats
  order_by = host_name ASC, user_sam ASC
end table
```

- **`formats`** — nested block; each line `column = preset` (preset = identifier, same set as `LabelFormat` / axis formats).
- **Presets** — `datetime.short`, `datetime.iso`, `date.short`, `date.iso`, `time.short`, `user.short`, `raw`, `system`, or report defaults via existing resolution.

### IR / Execution

Parser **normalizes** the `formats` block into one diagram property:

| Key | Value |
|-----|--------|
| `column_formats` | `bucket_start_utc:datetime.short, other:date.short` |

`TablePayloadBuilder` / `ColumnFormatMap` unchanged — block is authoring-only.

### Lint (future)

Validate `formats` keys ⊆ `columns` list.

## Consequences

- F# `TableDiagramParser` + [ADR-0071](DASHSPEC-ADR-0071-block-and-member-grammar.md) `ChildKeywordMerge` for `formats`.
- No Host change when IR contains synthesized `column_formats`.
