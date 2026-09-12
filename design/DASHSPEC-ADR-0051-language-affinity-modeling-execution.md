# DASHSPEC-ADR-0051: Language affinity — Modeling (F#) vs Execution (C#)

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-12 |
| **Tags** | #dashspec #modeling #execution #fsharp #csharp #language #parse #graph |
| **Relates to** | [ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) · [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) · [GUIDERS-FSHARP-ADR-0002](https://github.com/AI-Guiders/guiders-fsharp/blob/main/docs/adr/GUIDERS-FSHARP-ADR-0002-model-guild-fsharp-ownership.md) §13 |

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
- CommandPlane adapter, WitDB, admin surfaces, deploy and DI composition roots.

**Why F# for hard model:** parsing, graph structures, and algebraic invariants are **first-class** in F# (DU, `match`, immutability, parser pipelines). In C# they are **possible but unnatural** — the current `DashSpec.Core/Parsing/*` sprawl is the symptom, not an accident.

**Why C# for mechanics:** ASP.NET, Blazor, WPF, Roslyn tooling, plugin loading, and operator deploy paths are first-class in C#. Rewriting Host or connectors in F# is **out of scope**.

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

**Anti-pattern (reject in review):** growing `DashSpec.Core/Parsing/*` or nullable record fields for new grammar — that extends hard model in the wrong language.

**Transitional:** until M6 ([ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md)), bugfix-only touch of C# parsers; new grammar lands in F# Modeling from M2 onward.

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
- [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) migration phases M0–M8 are **mandated by language fit**, not optional refactor taste.
- [ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) surfaces (Host, Studio) remain thin **Execution** shells; Presentation extract is C# shared mechanics.
- Operators moving between `guiders-fsharp` and `dash-spec` see the **same F#/C# seam**, different grammar packages.

## Non-goals

- Arguing F# vs C# in abstract — decision is made; this ADR records **fit**, not a language shootout.
- «Modeling apps» or split deployables by model/mechanics — **all apps are Execution**.
- Rewriting Execution mechanics in F# (Host, connectors, CommandPlane runtime).
- Using this ADR to merge DashSpec grammar into federation GDL (deferred; [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) non-goals).

## References

- [GUIDERS-FSHARP-ADR-0002 §13](https://github.com/AI-Guiders/guiders-fsharp/blob/main/docs/adr/GUIDERS-FSHARP-ADR-0002-model-guild-fsharp-ownership.md) — federation F# affinity rule (precedent)
- [DASHSPEC-ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) — package map, migration M0–M8, federation boundary
- [DASHSPEC-ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) — Platform vs surfaces; Presentation extract
- `DashSpec.Core/Parsing/` — transitional C# hard model (port target)
