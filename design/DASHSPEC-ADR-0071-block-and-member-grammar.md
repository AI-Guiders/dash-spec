# DASHSPEC-ADR-0071: Block and member grammar (parse spine)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-30 |
| **Relates to** | [ADR-0036](DASHSPEC-ADR-0036-end-blocks-page-toolbar.md), [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md), [ADR-0070](DASHSPEC-ADR-0070-table-column-formats-block.md) |

## Context

Container parsing repeated the same loop: `beginBlock` → lines until `end <kind>` or `}` → schema `key = value` lines, plus occasional nested keywords (`formats`, `bind`, …). `PropertyBlockParser` and ad-hoc parsers (e.g. table) duplicated peek/merge logic.

## Decision

### Two container shapes (ADR-0036)

| Shape | Close | API |
|-------|--------|-----|
| **Keyword block** | `end <kind>` | `BlockGrammar.parseKeywordContainer` |
| **Bracket block** | `}` | `BlockGrammar.parseBracketContainer` |

Both share one body loop.

### Member grammar

**Member lines** — `MemberGrammar.readPropertyEntry` + `PropertySchemas` value types (`Scalar`, `RestOfLine`, `ColumnBinding`, …).

**Container members** — `BlockGrammar.ContainerMember`:

- `SchemaProperties(schema, allowExtension, allowQuotedKeys)` — zero or more `key = value` on one line, repeated per line.
- `ChildKeywordMerge(keyword, endKind, blockName, merge)` — nested `keyword` … `end keyword` whose body is `ident = <rest-of-line>` map, merged into parent IR via `StringMapMerge` (table `formats` → `column_formats`).

New nested blocks are declared as `ChildKeywordMerge` (or future `ChildKeyword` for arbitrary child parsers), not copy-pasted `while` loops.

### Legacy

No alternate surface for the same IR when a child block exists (e.g. no inline `column_formats = …` on table; [ADR-0070](DASHSPEC-ADR-0070-table-column-formats-block.md) updated).

## Consequences

- `PropertyBlockParser.parse` delegates to `BlockGrammar` with a single `SchemaProperties` member.
- `TableDiagramParser` is a member list: `formats` + table schema.
- C# `PropertyBlockParser` unchanged until a later port; F# is SSOT for diagram/table authoring extensions.
