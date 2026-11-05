---
type: ADR
id: DASHSPEC-ADR-0104
title: "Table columns pipe-table — `id | format | label | sorting`"
status: "Accepted"
date: 2026-10-20
tags: [dashspec, adr]
language: ru
relates:
  - DASHSPEC-ADR-0004
  - DASHSPEC-ADR-0057
  - DASHSPEC-ADR-0070
  - DASHSPEC-ADR-0071
---

# DASHSPEC-ADR-0104: Table columns pipe-table — `id | format | label | sorting`

## Context

`table` diagrams carried per-column metadata in three places: flat `columns = a, b, c`, a nested `formats` block (ADR-0070), and flat `order_by = "a ASC, b DESC"` (comma-list in quotes). ADR-0004 explicitly deferred labels for `table columns` («☐ comma-list, отдельный шаг»); ADR-0057 named GDL-таблицы among future authoring forms. The pipe-table body already exists in-repo: `TableRow` token in the lexer, `TableLiteral` (header + separator + row maps), `aliases` block as the first consumer, and federation `*.gdl` catalogs (`phrases table` / `commands table`).

Block bodies are contextual (same principle as flow edges): a block may carry its own row grammar. Attribute maps fit pipe tables; geometry stays in bracket rows (`[ a b c ]`, layout boards).

## Decision

### Canonical syntax (inside `table`)

```text
table
  columns
    | id                    | format         | label        | sorting |
    | host_name             | -              | Хост         | ASC     |
    | user_sam              |                | Пользователь | ASC     |
    | peak_bucket_start_utc | datetime.short | Момент пика  |         |
  end columns
end table
```

- **`columns` scalar or block** — flat `columns = a, b, c` stays the short form (no metadata); the pipe table is the full form. Both parse paths live in one `KeywordScalarOrBlock` member.
- **Header row** declares which attributes are present; vocabulary today: `id` (required, unique, row order = column order), `format` (same presets as `formats`), `label` (display text), `sorting` (`ASC`/`DESC`).
- **Unset cells** — `-` or empty.
- **Separator rows** `|---|---|` are skipped (`TableLiteral`).

### Desugar (parser → diagram IR properties)

| Attribute | IR property | Encoding |
|-----------|-------------|----------|
| `id` (row order) | `columns` | comma-list (unchanged consumers: `QueryCompiler` SELECT, `TablePayloadBuilder`) |
| `format` | `column_formats` | `serializeColumnFormatMap` (`k:v, k:v`) — same IR as the `formats` block |
| `label` | `column_labels` | newline `id = label` lines — **comma-safe** (labels may contain commas) |
| `sorting` (row order) | `order_by` | `id ASC, id DESC` joined |

### Conflicts are parse errors (never silent)

- flat `columns =` **and** the pipe table;
- flat `order_by` **and** `sorting`;
- `formats` block **and** the `format` attribute;
- unknown attribute, duplicate `id`, empty `id` cell, sorting ≠ ASC/DESC.

### Runtime / render

- `ColumnLabelMap.Parse` (mirror of the F# serializer) → `TablePayload.HeaderTexts` (parallel list; `null` when no labels).
- `TableHtmlCardViz` renders `HeaderTexts[i] ?? Columns[i]` in `thead`; column **ids stay internal** (per-column search/filter binds by id; export keeps ids).

## Consequences

- `BlockGrammar`: `readPipeTableRows` (TableRow rows until `end <kind>`) + `serializeColumnLabelMap` (newline map).
- `TableDiagramParser`: `KeywordScalarOrBlock("columns", …)` + post-parse desugar.
- Future per-column attributes (width, visibility, export preset) extend the header vocabulary — no new mechanics.
- `formats` block and flat forms remain valid synonyms; migration is per-file, not forced.
- Lint (future, shared with ADR-0070): validate `format` presets and `id` against the data source schema.
