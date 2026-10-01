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

**Context-bound syntax** — surface that reuses tokens but **different semantics** (e.g. `ident:value` as layout **cell weight**, `col:preset` in formats IR, GDL wire literals) **MUST NOT** live on generic `MemberGrammar`. Put parsers in the **domain module** (e.g. `LayoutBracketRowParser`) and wire them via `BlockGrammar` members that take an explicit `parseRow` / `readValue` delegate. `MemberGrammar` stays **context-free** (`key = value`, string maps with an injected value reader, ident maps).

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

### Container member kinds

| Member | Use |
|--------|-----|
| `SchemaProperties` | `key = value` lines from `PropertySchemas` |
| `ChildKeywordMerge` | Nested `keyword` … `end keyword` string map → serialized IR property (table `formats`) |
| `ChildKeyword` | Nested block or keyword line with custom parser (`filter bind labels`, reject `default`) |
| `KeywordScalarOrBlock` | `keyword = scalar` or `keyword` … `end keyword` (`filters chrome apply`) |
| `RepeatingBracketRows(rows, parseRow)` | Repeated `[ … ]` rows; **`parseRow` is context-specific** (layout: `LayoutBracketRowParser.parse`) |

`parseKeywordContainer` accepts optional `endId` for `end link <id>`-style closes.

### Migrated to BlockGrammar / MemberGrammar (F#)

| Surface | Parser |
|---------|--------|
| Generic property blocks | `PropertyBlockParser` → `BlockGrammar` |
| Table `formats` | `TableDiagramParser` |
| `filters chrome` | `FiltersChromeParser` |
| `filter` bind (schema + `labels`) | `FilterParser.parseStructuredBindBlock` |
| Host `link` | `HostModuleParser.parseLink` |
| Card / cards `chrome` | `CardChromeParser`, `CardsChromeParser` |
| Toolbar `commands` | `CommandAliasesParser` → `parseIdentMapBlock` |
| Tooltip `variables` | `TooltipModuleParser` |
| Display `bind` slots | `DisplayBindingParser` → `parseStringMapBody` |
| Layout `group` / `nest` bodies | `LayoutParser` → `RepeatingBracketRows` + `layoutGroup` schema |
| Layout board rows `[ … ]` | `LayoutBracketRowParser` (cell `ref:weight` only inside `[ ]`) |

### Still bespoke (nested DSL, not flat members)

Card/report/document shells, layout **board** entry stream (`[ ]` vs `group` vs `nest`), filter `filter` wrapper (`bind`/`show`/`place` children), diagram module includes, toolbar placement, catalog, defaults tree, palette — orchestration stays in module parsers; **member lines** inside schema-only bodies use `MemberGrammar` / `BlockGrammar`.
