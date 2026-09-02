# DASHSPEC-ADR-0048: Modeling vs Execution — planet DSL split (F# parse)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-02 |
| **Tags** | #dashspec #planet #modeling #execution #fsharp #dsl #parse |
| **Relates to** | [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md) · [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) · [ADR-0036](DASHSPEC-ADR-0036-end-blocks-page-toolbar.md) · [ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) · [GUIDERS-ADR-0048](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0048-authoring-quarry-family.md) (Authoring Guild — federation) · [GUIDERS-FSHARP-ADR-0002](https://github.com/AI-Guiders/guiders-fsharp/blob/main/docs/adr/GUIDERS-FSHARP-ADR-0002-model-guild-fsharp-ownership.md) · [CDP-ADR-0208](https://github.com/AI-Guiders/cdp-mcp/blob/main/docs/adr/CDP-ADR-0208-language-code-ir-fsharp-fcs.md) (informative — LRC) |

## Context

`DashSpec.Core` today mixes **declare-time** and **runtime** concerns in one C# assembly:

| Area | Today (`DashSpec.Core`) | ~scale |
|------|-------------------------|--------|
| Lexer + parsers (`Parsing/*`) | hand-rolled recursive descent, `TryKeyword` chains | ~56 files · ~8.5k LOC |
| IR (`Model/*`) | records (`DashboardDocument`, `CardDefinition`, …) | ~22 files |
| Resolve / effective model | `Resolution/`, `Parsing/IncludeExpander`, `SpecResolver` | mixed with parse |
| Runtime bind / payloads | `Runtime/`, `Compilation/QueryCompiler` | C# — correct layer |
| Project / imports | `Authoring/DashSpecProject` → federation `Authoring.Core` | thin C# shim |

Problems:

- **Parser sprawl** — `CardParser` and peers are large hand-rolled `TryKeyword` ladders with mutable parse state; hard to extend and test in isolation.
- **No algebraic IR boundary** — optional card children are nullable fields + runtime throws; new keywords need manual ladder updates across many files.
- **Diagnostics** — `DashSpecParseException` throw-on-error; federation Authoring uses accumulated `AuthoringDiagnostic` lists.
- **Layer blur** — [ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) names Platform vs Surfaces; **inside** platform, parse/IR still shares a package with session builders and SQL compile.

Federation split ([GUIDERS-FSHARP-ADR-0002](https://github.com/AI-Guiders/guiders-fsharp/blob/main/docs/adr/GUIDERS-FSHARP-ADR-0002-model-guild-fsharp-ownership.md)):

```text
Platform.Modeling.*     F#   parse, IR SSOT, validation, conformance
Platform.Execution.*    C#   mechanics, UI, session, emit — consumers only
```

DashSpec owns `.dashspec` / `.dashdiagram` / … grammar in-repo ([GUIDERS-ADR-0048](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0048-authoring-quarry-family.md) §Planet boundary — federation does not own dashspec bodies). This ADR applies the **Modeling / Execution** seam inside dash-spec only. GDL federation promotion, file renames, and public authoring narrative are **out of scope** until Studio ships.

## Decision

### 1. Planet package prefix (normative)

```text
DashSpec.Modeling.*     F#   grammar, AST SSOT, parse, validation, conformance vectors
DashSpec.Execution.*    C#   resolve, bind, session, query compile, runtime payloads, connectors
```

**Dependency rule:** `DashSpec.Execution.*` → `DashSpec.Modeling.*`. Never a second authoritative AST in Execution.

Surfaces ([ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md)) and `DashSpec.Host` remain **Execution consumers** — they do not own grammar.

```text
dash-spec repo (planet platform)
├── src/DashSpec.Modeling.Core          F#  IR spine, diagnostics, source spans
├── src/DashSpec.Modeling.Parse         F#  lexer, token reader, block syntax, parsers (ADR-0017 roots)
├── src/DashSpec.Execution.Core         C#  resolve, effective model, workspace index
├── src/DashSpec.Execution.Runtime      C#  bind, QueryCompiler, MatrixPayloadBuilder, …
├── src/DashSpec.Execution.Catalog      C#  dashcatalog, git sync (when split from Host)
├── src/DashSpec.Abstractions           C#  ports (IReportSession, …) — unchanged role
├── src/DashSpec.Host                   C#  web surface — thin
└── src/DashSpec.LanguageServer         C#  LSP host → calls Modeling.Parse + Execution.Core
```

`DashSpec.Core` becomes a **transitional shim** (re-exports Execution + Modeling interop) until downstream planets update package refs — then removed.

### 2. Modeling owns (F#)

| Package | Owns | Replaces (transitional C#) |
|---------|------|---------------------------|
| `DashSpec.Modeling.Core` | `DashSpecDocument` spine, file-kind DU, `DashSpecDiagnostic`, `SourceSpan` | `Model/*` records → F# DU + `[<CLIMutable>]` export where C# needs records |
| `DashSpec.Modeling.Parse` | all `Parsing/*`, `Lexing/*`, include graph at parse boundary | ~8.5k LOC C# parsers |
| `DashSpec.Modeling.Validation` (optional v2) | cross-file rules, layout scope, SQL-readonly lint at declare-time | `Validation/*`, parts of `Analysis/` |

**File roots** (planet SSOT — [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md)):

```text
.dashspec · .dashdiagram · .dashlayout · .dashpalette · .dashpresentation
.dashtransform · .dashinclude · .dashcatalog · .dashtooltip · …
```

One Modeling parse entry per root kind; **planet-owned** lexer + token layer (`@` roots, `end kind id`, `end on click`, same-line properties) — richer than federation GDL `BlockReader` today (line-level `end keyword` only).

### 3. Execution owns (C#)

| Package | Owns | Stays C# because |
|---------|------|------------------|
| `DashSpec.Execution.Core` | `SpecResolver`, effective model merge, `DashSpecWorkspaceIndex`, `DashSpecProject` orchestration | IO, host integration, federation `AuthoringProjectLoader` interop |
| `DashSpec.Execution.Runtime` | filter bind, chart/matrix payload builders, diagram plugin resolution | runtime mechanics, connector calls |
| `DashSpec.Execution.Compilation` | `QueryCompiler`, parameterized SQL | integrates with connectors |
| `DashSpec.Connectors.*` | SqlServer, … | unchanged |
| `DashSpec.Host` / Studio | surfaces | [ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) |

Execution **maps** Modeling IR to runtime DTOs — does not re-parse text except through Modeling APIs.

### 4. Federation boundary (reference, not merge)

| | Federation (Authoring Guild) | DashSpec (this repo) |
|--|------------------------------|----------------------|
| Grammar | `*.{quarry}.gdl` | `.dashspec`, `.dashdiagram`, … |
| Modeling packages | `Platform.Modeling.Gdl.*` | `DashSpec.Modeling.*` |
| Block / parse kit | `BlockReader` (line-level; catalog quarries) | **not used** — premature; own token layer |
| Project imports | `AuthoringProjectLoader` / `GdlProject` | `DashSpecProject` may keep thin C# shim → federation |
| CDP LRC | `GdlBackend` | future planet backend (informative; [CDP-ADR-0208](https://github.com/AI-Guiders/cdp-mcp/blob/main/docs/adr/CDP-ADR-0208-language-code-ir-fsharp-fcs.md)) |

DashSpec **does not** take a dependency on federation Block Kit in M0–M6. Lexical overlap (`end keyword`, `#` comments) is coincidental, not a shared library contract. If GDL later needs token-aware blocks (`end card id`, `@` roots), federation **may** study DashSpec Modeling — not the other way around today.

DashSpec **does not** copy `GdlFragment` or adopt federation quarry names in this ADR.

### 5. F# rationale (planet choice)

F# is **recommended** for `DashSpec.Modeling.*`, not mandated for Execution:

- block/module grammar is **algebraic** — DU + exhaustive `match` vs 900-line `TryKeyword` ladders;
- diagnostic accumulation (ref list, partial AST) vs throw-on-error;
- planet-owned **token lexer** — no forced fit to federation line-level `BlockReader`;
- Host / Blazor / connectors stay idiomatic C# with `[<CLIMutable>]` or thin mapper at the boundary.

**Language per layer is a planet decision** — federation precedent is F# Modeling + C# Execution, not a platform law for sovereign DSLs.

### 6. Public API stability

v0 consumers (`DashSpecParser.Parse`, `DashboardDocument`) remain on **`DashSpec.Execution.Core`** facade:

```csharp
// Execution facade — stable for Host, Studio, tests
public static class DashSpecParser
{
    public static DashboardDocument Parse(string text, string? specDirectory = null, …)
        => DashSpecExecutionPipeline.Parse(text, specDirectory, …);
}
```

Internal flow:

```text
text ──► Modeling.Parse ──► DashSpecDocument (F#)
              │
              ▼
       Execution.Core maps ──► DashboardDocument (C# record, transitional)
              │
              ▼
       Execution.Runtime / session
```

Long-term: expose Modeling IR types to Studio/LSP; C# `DashboardDocument` becomes a projection or `[<CLIMutable>]` export — not a second SSOT.

### 7. Interop rules

- F# Modeling types crossing to C#: `[<CLIMutable>]` records or explicit mapper module in `Execution.Core`.
- F# tests own conformance vectors (`tests/DashSpec.Modeling.Parse.Tests`).
- C# integration tests stay on `Execution.*` facades (`DashSpec.Core.Tests` → rename).

## Migration phases

```text
M0  ADR-0048 + solution scaffold (first F# projects in dash-spec)
M1  DashSpec.Modeling.Core — diagnostic + span types; parity with DashSpecDiagnostic
M2  DashSpec.Modeling.Parse — pilot: .dashlayout or .dashcatalog (smallest roots)
M3  Execution.Core extracts SpecResolver; references Modeling.Parse
M4  Port .dashspec / @dashboard / @card (largest surface — incremental per parser file)
M5  Port fragment kinds (.dashdiagram, .dashpalette, …)
M6  Remove C# Parsing/*; DashSpec.Core shim → split packages
M7  Modeling.Validation + CLI `dashspec validate` ([ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) v1.1)
M8  optional CDP LRC backend for `.dashspec` (planet extension; not federation scope)
```

**Non-big-bang:** new grammar work lands in F# Modeling from M2 onward; C# parsers touched only for bugfix until ported.

## Consequences

- Clear seam aligned with federation Modeling/Execution — easier for operators moving between guiders-fsharp and dash-spec.
- Parser maintenance cost drops as F# DU + token-layer patterns replace C# sprawl.
- [ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) package map updates: `DashSpec.Core` → `Modeling.*` + `Execution.*`.
- Studio / Host / LSP share one parse SSOT; surfaces stay thin.
- Internal refactor only — no product commitment on GDL rebrand or public authoring until Studio.

## Non-goals

- Merging `.dashspec` into federation `*.{quarry}.gdl` or GDL «edition» branding (deferred — post-Studio discussion).
- Rewriting Host, connectors, or EF in F#.
- Mandatory CDP integration in M0–M6.
- Taking a dependency on federation `BlockReader` / Authoring block kit (premature — dashspec grammar is token-rich).
- Deleting C# `DashboardDocument` in v1 of the split (projection/shim until consumers migrate).

## References

- [GUIDERS-FEDERATION-CONSTITUTION](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/GUIDERS-FEDERATION-CONSTITUTION.md) — planets are not federation SSOT
- [GUIDERS-ADR-0048](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0048-authoring-quarry-family.md) — Authoring Guild vs planet grammars
- [GUIDERS-FSHARP-ADR-0002](https://github.com/AI-Guiders/guiders-fsharp/blob/main/docs/adr/GUIDERS-FSHARP-ADR-0002-model-guild-fsharp-ownership.md) — federation Modeling/Execution precedent (pattern only)
- `DashSpec.Core/Parsing/` — current C# parser surface (~8.5k LOC); `BlockSyntax.cs` / `TokenReader.cs` — port targets for F# Modeling
