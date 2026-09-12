# DASHSPEC-ADR-0051: Language affinity — Modeling (F#) vs Execution (C#)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-12 |
| **Tags** | #dashspec #modeling #execution #fsharp #csharp #language #parse #graph |
| **Relates to** | [ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) · [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) · [GUIDERS-FSHARP-ADR-0002](https://github.com/AI-Guiders/guiders-fsharp/blob/main/docs/adr/GUIDERS-FSHARP-ADR-0002-model-guild-fsharp-ownership.md) §13 · [GUIDERS-ADR-0059](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0059-gdl-hyperlane.md) (`gdlc` emit) · [GUIDERS-ADR-0047](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0047-command-for-doi.md) (`.catalog`) |

## Context

DashSpec today ships ~8.5k LOC of hand-rolled C# parsers inside `DashSpec.Core` — `TryKeyword` ladders, nullable IR fields, throw-on-error diagnostics. That code works, but it fights the language: every new construct spreads across many files, optional children become null checks, graph-shaped resolve blurs with session mechanics.

Federation already decided the same thing for GDL + Notations ([GUIDERS-FSHARP-ADR-0002](https://github.com/AI-Guiders/guiders-fsharp/blob/main/docs/adr/GUIDERS-FSHARP-ADR-0002-model-guild-fsharp-ownership.md) §13 **F# affinity rule**):

> Default **F#** when the module is predominantly: discriminated unions + exhaustive `match`; parse → validate → IR pipeline; pure graph transform; conformance vectors over algebra.  
> Default **C# Execution** when the module is predominantly: IO, async, UI hosts, DI, MCP, `Publish`/`Subscribe`, Roslyn emit.

**This ADR states the first principle for dash-spec.** [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) is the **how** (packages, migration M0–M8, federation boundary). This ADR is the **why**.

The split is **not**:

- a product topology («viewer app» vs «authoring app» vs «Modeling app»);
- a planet-branding exercise («what DashSpec means»);
- a mandate to merge `.dashspec` into federation GDL.

The split **is**:

- **language fit** — hard model work belongs where the language is natural; mechanics belong where the ecosystem is natural.

## Decision

### 1. First principle (normative)

```text
Hard model     → F#     parse, AST, validation, graph algebra, conformance
Mechanics      → C#     resolve orchestration, session, SQL compile, UI, connectors, deploy
```

**Hard model** includes:

- lexing and parsing (token layers, block syntax, include graphs at parse boundary);
- algebraic IR (discriminated unions, exhaustive cases, invariant laws);
- pure graph transforms (effective model merge, layout scope, cross-file validation);
- accumulated diagnostics (partial AST + error list, not exception-driven control flow);
- conformance vectors and property tests over the model.

**Mechanics** includes:

- `IReportSession`, filter bind, payload builders, diagram plugin resolution;
- `QueryCompiler`, connector calls, EF/SqlClient, git catalog sync runtime;
- Blazor Host, WPF Studio shell, LSP host process, CLI entrypoints;
- WitDB, admin surfaces, deploy and DI composition roots;
- **Federation platform mechanics consumed by the planet** — not reimplemented in dash-spec (see §1b).

**Mechanics does not mean «everything invented in dash-spec».** Slash commands, channels, phrase slots, keyboard grammars, and CommandPlane registry/execute/completion come from **federation GDL + `Platform.Execution.*`** ([GUIDERS-ADR-0047 command catalog](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0047-command-for-doi.md)). The planet ships `.catalog.gdl` / `.deck.gdl` meaning and thin C# glue (context expanders, executors); federation ships the mechanics stack.

**Why F# for hard model:** parsing, graph structures, and algebraic invariants are **first-class** in F# (DU, `match`, immutability, parser pipelines). In C# they are **possible but unnatural** — the current `DashSpec.Core/Parsing/*` sprawl is the symptom, not an accident.

**Why C# for mechanics:** ASP.NET, Blazor, WPF, Roslyn tooling, plugin loading, CommandPlane runtime, and operator deploy paths are first-class in C#. Rewriting Host, connectors, or CommandPlane in F# is **out of scope**.

### 1b. Federation GDL mechanics (normative — not planet-owned)

Planet apps **consume** federation Execution; they do **not** own slash/CommandPlane infrastructure.

```text
*.{quarry}.gdl  (catalog, deck, …)
        │  parse → quarry IR          Modeling (F# target; C# Authoring transitional)
        ▼
Platform.Execution.CommandPlane.*   registry · execute · completion · constructors
        │  emit --lang=cs (optional)  →  *.g.cs partials
        ▼
Planet C# Execution glue            context expanders · executors · session wiring
```

| Artifact | SSOT | Planet today | Target consumer pattern |
|----------|------|--------------|-------------------------|
| `dash.catalog.gdl` | federation catalog quarry | runtime parse in `DashboardCatalog` | **emit** → `DashCatalog.g.cs` + MSBuild regen ([GUIDERS-ADR-0059](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0059-gdl-hyperlane.md) §10) |
| `dashspec-studio.deck.gdl` | federation deck quarry | `deck emit` → `DeckIds.g.cs` ✓ | same |
| `.dashspec` / fragments | planet grammar ([ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md)) | monolith `DashSpec.Core` | `DashSpec.Modeling.*` + `DashSpec.Execution.*` |

**Emit tooling today (transitional, not unified `gdlc` yet):**

| Quarry | CLI | Emitter |
|--------|-----|---------|
| `.catalog` | `authoring emit` ([authoring-toolchain](https://github.com/AI-Guiders/authoring-toolchain)) | `CatalogCatalogEmitter` |
| `.deck` | `deck emit` ([DeckEmit](https://github.com/AI-Guiders/guiders-assist)) | `Surface.Wpf.CodeGen` |

Unified `gdlc` ([GUIDERS-ADR-0059](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0059-gdl-hyperlane.md)) is **infrastructure follow-up**, not a gate for planet Execution split. **Gate:** MSBuild/CI regen for `.catalog.gdl` → `*.g.cs` and elimination of hand-duplicated catalog partials (`DashboardCatalogFlavor`, bindings constants) that drift from GDL.

**Planet-only C# (correct Execution):** `DashboardCommandCatalogExpander` — binds runtime context (reports, pages, toolbar filters) to federation command ids. **Anti-pattern:** reimplementing CommandPlane, slash completion, or phrase-slot algebra in dash-spec.

### 2. Package prefix (normative)

Same seam as federation; planet-owned grammar ([ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md)):

```text
DashSpec.Modeling.*     F#   grammar, IR SSOT, parse, validation, conformance
DashSpec.Execution.*    C#   resolve, bind, session, query compile, runtime, surfaces
```

**Dependency rule:** `DashSpec.Execution.*` → `DashSpec.Modeling.*`. Never a second authoritative AST in Execution.

Execution MAY hold `[<CLIMutable>]` projections or thin mappers at the seam; MUST NOT fork IR shapes.

### 3. Deployables — all Execution (normative)

Every shipped executable or surface is **C# Execution**:

| Deployable | Role | Modeling? |
|------------|------|-----------|
| `DashSpec.Host` | web consumption viewer | **No** — consumes Execution session |
| `DashSpec Studio` | desktop authoring viewer | **No** — consumes Execution + Presentation |
| `DashSpec.LanguageServer` | LSP host | **No** — calls Modeling APIs through Execution/LSP glue |
| `dashspec validate` (roadmap) | CLI | **No** — orchestrates Modeling.Validate via Execution CLI |
| Planet repos (URSA, LUF) | `.dashspec` content + deploy | **No** — content only; platform NuGet for runtime |

There are **no Modeling applications**. Modeling ships as **libraries** referenced by Execution.

Studio deck `.gdl` chrome is federation surface authoring ([GUIDERS-ADR-0055](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0055-surface-wpf-guild-deck-authoring.md)) — not dashspec grammar; still not a Modeling deployable.

### 4. Symptom rule (lint for reviews)

When adding or extending dash-spec code, default placement:

| Signal in the change | Belongs in |
|----------------------|------------|
| New keyword / block / `@` root / `end kind id` | `DashSpec.Modeling.Parse` (F#) |
| New card/tab/filter **shape** in IR | `DashSpec.Modeling.Core` (F#) |
| Cross-file scope / include cycle / layout law | `DashSpec.Modeling.Validation` (F#) |
| Effective merge orchestration, file watcher, workspace index IO | `DashSpec.Execution.Core` (C#) |
| Filter bind, matrix payload, chart plugin pick | `DashSpec.Execution.Runtime` (C#) |
| Blazor component, WPF zone, WebView2 host wiring | `DashSpec.Execution.*` or `DashSpec.Presentation` (C#) |
| New slash command / phrase / channel / binding row | `Catalog/*.catalog.gdl` (GDL SSOT) — **not** new C# registry tables |
| Command executor / runtime context expansion | Planet C# partial + `Platform.Execution.CommandPlane` |
| Deck zone / preset id | `*.deck.gdl` + `deck emit` — **not** hardcoded string constants |

**Anti-pattern (reject in review):** growing `DashSpec.Core/Parsing/*` or nullable record fields for new grammar — that extends hard model in the wrong language.

**Anti-pattern (reject in review):** hand-maintained catalog constants parallel to `dash.catalog.gdl` — use emit or runtime parse from GDL, not both drifting.

**Transitional:** until M6 ([ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md)), bugfix-only touch of C# parsers; new grammar lands in F# Modeling from M2 onward.

### 4b. Infrastructure before applications (normative sequencing)

**Surfaces and planet apps ship only after platform infrastructure slices are green.** Do not treat Studio v0 or Host slimming as the driver of Modeling/Execution split — they are **consumers** that prove the seam.

```text
Phase I — federation GDL consumer path (dash-spec + studio)
  I1  catalog emit: dash.catalog.gdl → DashCatalog.g.cs + MSBuild/CI regen
  I2  eliminate dual catalog maintenance (runtime-only partials vs GDL)
  I3  deck emit already ✓ (studio); document in gdlproj/csproj wiring

Phase II — dash-spec platform split ([ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md))
  II1 Execution.* extract from DashSpec.Core (C# — can start before F#)
  II2 DashSpec.Modeling.* scaffold M0–M2 (F# pilot quarries)
  II3 Presentation extract ([ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md))

Phase III — surfaces (applications)
  III1 Studio v0: Execution + Presentation + generated catalog/deck
  III2 Host slim: consumption viewer; slash from generated catalog + CommandPlane
  III3 session parity tests (Host vs Studio Preview)
```

**Gate for Phase III:** Phase I catalog emit regen **done**; Phase II at least `Execution.*` package split + `DashSpecParser` facade stable. Studio WebView2→Host URL embed is **not** acceptable as Report Preview done ([ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md)).

Unified `gdlc` binary and F# catalog parse (`Platform.Modeling.Catalog`) are **parallel infrastructure** — improve Phase I/II but do not block I1 if `authoring emit` + MSBuild target suffice.

### 5. Relationship to federation

| | Federation | DashSpec (this planet) |
|--|------------|------------------------|
| Rule | [GUIDERS-FSHARP-ADR-0002](https://github.com/AI-Guiders/guiders-fsharp/blob/main/docs/adr/GUIDERS-FSHARP-ADR-0002-model-guild-fsharp-ownership.md) §13 | **Same rule** — this ADR |
| Modeling packages | `Platform.Modeling.*` | `DashSpec.Modeling.*` |
| Execution packages | `Platform.Execution.*` | `DashSpec.Execution.*` |
| Grammar | `*.{quarry}.gdl` | `.dashspec`, `.dashdiagram`, … |
| Shared library | — | **No** BlockReader merge in M0–M6 ([ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) §4) |

Federation and DashSpec share **language affinity**, not a shared parse library.

### 6. Public API (unchanged intent from ADR-0048)

Stable consumer surface stays on **Execution**:

```csharp
// Host, Studio, tests, LSP — C# only
DashSpecParser.Parse(text, specDirectory, …);
```

Internal: `text → Modeling.Parse → F# IR → Execution maps → session/runtime`.

## Consequences

- Code review and agent work have a **single litmus test**: hard model → F#; mechanics → C#.
- **Slash / CommandPlane / catalog / deck** mechanics come from **federation GDL + Platform.Execution** — dash-spec adds planet `.gdl` + thin C# glue only.
- [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) migration phases M0–M8 are **mandated by language fit**, not optional refactor taste.
- [ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) surfaces (Host, Studio) ship in **Phase III** — after catalog emit regen and Execution package split (**§4b**).
- Operators moving between `guiders-fsharp` and `dash-spec` see the **same F#/C# seam**, different grammar packages.

## Non-goals

- Arguing F# vs C# in abstract — decision is made; this ADR records **fit**, not a language shootout.
- «Modeling apps» or split deployables by model/mechanics — **all apps are Execution**.
- Rewriting federation CommandPlane or slash stack inside dash-spec.
- Rewriting Host/connectors **runtime** in F#.
- Using this ADR to merge DashSpec grammar into federation GDL (deferred; [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) non-goals).
- **Studio v0 or Host features as a substitute for Phase I–II infrastructure** — apps follow infra, not the reverse.

## References

- [GUIDERS-FSHARP-ADR-0002 §13](https://github.com/AI-Guiders/guiders-fsharp/blob/main/docs/adr/GUIDERS-FSHARP-ADR-0002-model-guild-fsharp-ownership.md) — federation F# affinity rule (precedent)
- [GUIDERS-ADR-0059](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0059-gdl-hyperlane.md) — `gdlc` emit pipeline; `*.g.cs` consumer pattern
- [GUIDERS-ADR-0047 command catalog](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0047-command-for-doi.md) — `.catalog.gdl` quarry
- [authoring-toolchain](https://github.com/AI-Guiders/authoring-toolchain) — `authoring emit` for catalog
- [DASHSPEC-ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) — package map, migration M0–M8, federation boundary
- [DASHSPEC-ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) — Platform vs surfaces; Presentation extract
- `DashSpec.Host/Catalog/dash.catalog.gdl` — federation catalog SSOT (emit target)
- `DashSpec.Core/Parsing/` — transitional C# hard model (port target)
