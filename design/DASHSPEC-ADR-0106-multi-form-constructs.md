# DASHSPEC-ADR-0106: Multi-form constructs — `Construct.declare`

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-20 |
| **Relates to** | [ADR-0104](DASHSPEC-ADR-0104-table-columns-pipe-table.md) / [ADR-0105](DASHSPEC-ADR-0105-filters-pipe-table.md) (pipe-table family), [ADR-0071](DASHSPEC-ADR-0071-block-and-member-grammar.md) |

## Context

The pipe-table family (ADR-0104 columns, ADR-0105 filters) made each construct pay twice:
both surface parsers are hand-rolled and connected by hand-written desugar glue. In
`FilterTableParser` the equivalence knowledge is smeared by hand: the attribute list is
duplicated (`allowedAttributes` next to the block parser's own vocabulary), every cell is
hand-extracted, and the `FilterDefinition` construction is written a second time next to
`FilterParser.parse`. Nothing in the kernel says "these two forms express the same thing".

The family earns its third real instance (sourced clicks, ADR-0107) — the moment the
equivalence becomes explicit instead of re-glued per construct.

## Decision

The kernel gains a construct declaration:

```fsharp
Construct.declare name
    vocabulary        // columns + cell rules — written ONCE
    blockProjection   // how nested statements fill the same cells (per-construct)
    rowConstructor    // cells -> IR + validateSemantics — ONE semantics path
```

- `parseConstruct` dispatches by surface: bare pipe rows → the table form; otherwise →
  the block projection. Both fill the same row (column → cell).
- Kernel-shared for both forms: header-declares-columns, `-`/empty = unset, separator
  rows skipped, vocabulary validation, one construction + one semantic validation.
- **The block projection stays per-construct**: block shapes genuinely differ (filters
  have nested `bind`/`show`, clicks have effect statements, columns have props). What
  becomes single is the table side, the vocabulary, and the construction. Not "zero
  block code" — "one block code, zero table code, one vocabulary".
- Migrations (this ADR, not deferred): `filters` (ADR-0105) compresses
  `FilterTableParser` onto the declaration — the attribute list deduplicates with the
  block parser; `columns` (ADR-0104) migrates its desugar onto the declaration.
- Both forms stay valid per the family rule (legacy coexists, file-by-file).

## Consequences

- New constructs (clicks now, `when` next) declare once and get both forms.
- The two-forms-one-semantics statement lives in one place per construct, in code.
- The IR is unchanged — the same records come out of either form; existing desugar
  tests keep passing.
