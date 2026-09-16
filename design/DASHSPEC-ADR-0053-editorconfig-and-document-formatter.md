# DASHSPEC-ADR-0053: EditorConfig floor + DashSpec document formatter (MLP)

| Field | Value |
|-------|-------|
| **Status** | Accepted |
| **Date** | 2026-09-16 |
| **Relates to** | [ADR-0036](DASHSPEC-ADR-0036-end-blocks-page-toolbar.md) · [STUDIO-ADR-0004](https://github.com/AI-Guiders/dash-spec-studio/blob/main/design/STUDIO-ADR-0004-editorconfig-studio-wire.md) |

## Context

DashSpec uses Basic-like blocks (`card` … `end card`, `report` … `end report`). EditorConfig universal keys cover indent/EOL hygiene only; there is no industry standard for `end`-keyword block layout. Studio and LSP hosts need a **single SSOT** for document text hygiene and structural reindent.

## Decision

### 1. Two-layer model (Prettier pattern)

| Layer | Source | Responsibility |
|-------|--------|----------------|
| **Universal floor** | `.editorconfig` | `indent_*`, `end_of_line`, `trim_trailing_whitespace`, `insert_final_newline` |
| **DashSpec opinion** | `DashSpecBlockFormatter` in `DashSpec.Modeling.Parse` (bridge via `DocumentFormatBridge`) | Block-aware reindent + blank-line policy |

No custom brace-alignment keys — DashSpec is not brace-primary (ADR-0036 `end` syntax).

### 2. Universal keys (honored by resolver)

Applied to document surfaces: `*.{dashspec,dashlibrary,dashlayout,dashdiagram,dashpalette,dashcatalog,dashtooltip}` and Studio siblings (`*.sql`, `*.toml`).

### 3. `dashspec_*` keys (formatter-only)

| Key | Default | Effect |
|-----|---------|--------|
| `dashspec_format_on_save` | `true` | Save runs formatter + hygiene |
| `dashspec_max_consecutive_blank_lines` | `1` | Collapse vertical whitespace |
| `dashspec_indent_block_body` | `true` | +1 indent level after block opener lines |
| `dashspec_preserve_blank_line_before_end` | `true` | Ensure blank line before `end …` when body non-empty |
| `dashspec_blank_line_between_blocks` | `true` | Blank line after `end …` before next sibling opener/content at same indent |

New `dashspec_*` keys require formatter rule + test in the same PR.

### 4. Public API (`DashSpec.Core.Authoring`)

- `EditorConfigResolver.ResolveForFile(absolutePath, searchRoot?)`
- `DashSpecDocumentPipeline.Format(text, absolutePath, searchRoot?)`
- `DashSpecDocumentPipeline.PrepareForSave(text, absolutePath, searchRoot?)`

LSP `textDocument/formatting` should call the same pipeline (thin glue, non-goal this leaf).

### 5. Block layout (canonical)

- `@dashboard` / module headers at column 0; body at +1 level per nesting.
- Block openers (`runtime`, `report`, `card`, `bind`, `layout grid`, `toolbar chrome`, `on click`, …) emit at current depth; body lines +1 when `dashspec_indent_block_body = true`.
- `end <kind>` at opener depth (Basic-like), not parent depth.
- Legacy `{` / `}` blocks: depth +/- 1 (ADR-0036 dual syntax).

### 6. Non-goals

- Semantic reorder (cards, filters).
- `max_line_length` wrap.
- Roslyn `csharp_*` keys (C# uses federation SSOT when LanguageIntelligence ships).
- Custom `dashspec_align_closing_brace` — rejected (no EditorConfig standard; DSL is `end`-primary).

## Verification

- `DashSpec.Core.Tests`: resolver glob merge, formatter golden, `demo-soak.dashspec` parse round-trip after format.
- Studio: save + Ctrl+Shift+F (STUDIO-ADR-0004).

