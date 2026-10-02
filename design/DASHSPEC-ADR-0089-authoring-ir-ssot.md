# DASHSPEC-ADR-0089: Authoring IR SSOT and federation boundary

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-10-03 |
| **Relates to** | [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md), [ADR-0076](DASHSPEC-ADR-0076-architecture-build-guards.md) |

## Context

Authoring graph, invariant laws, and semantic parse entry were placed in `DashSpec.Modeling.CodeCenter`, which references federation `AIGuiders.Platform.Modeling.CodeCenter`. That made **planet Modeling depend on Code Center** — wrong direction.

## Decision

1. **`DashSpec.Modeling.Authoring`** — planet-only: concept graph, rules, `DashSpecAuthoringEntry.parse` (surface projection + `DashboardDocument` when parse succeeds). No federation references.

2. **`DashSpec.Modeling.CodeCenter`** — **adapter only**: `IDocumentLanguageProfile`, session factory, profile rebuild into federation `DocumentSnapshot`. Depends on Authoring + Platform.CodeCenter.

3. **`DashSpec.CodeCenter.Plugin`** — WPF surface; references adapter + federation.

4. **Enforcement** — NetArchTest in `DashSpec.Architecture.Tests`: `DashSpec.Modeling.Parse`, `.Core`, `.Authoring` must **not** reference `AIGuiders.Platform.Modeling.CodeCenter`. Not Roslyn string guards on parse method names.

## Consequences

- `SyntaxTree.parse` remains outline projection inside Parse; semantic entry is `DashSpecAuthoringEntry`.
- Long-term: spans on IR nodes; concept graph indexes IR instead of parallel outline AST.
