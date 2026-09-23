# DASHSPEC-ADR-0052: Catalog emit drift gate (W0)

| Field | Value |
|-------|-------|
| **Status** | Accepted |
| **Date** | 2026-09-13 |
| **Relates to** | [ADR-0051](DASHSPEC-ADR-0051-language-affinity-modeling-execution.md) §4b I1 · [GUIDERS-ADR-0059](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0059-gdl-hyperlane.md) |

## Context

Phase I1 requires `dash.catalog.gdl` → `DashCatalog.g.cs` emit with MSBuild regen. Without a CI gate, hand-edits or stale commits drift from `gdlc emit` output (AP-02 anti-pattern in ADR-0051).

Wave 2.5 wiring (`authoring/dashspec.gdlproj`, `Platform.Gdl.Emit`) landed in Host; this ADR records the **drift gate** only — not Planet Split Modeling (W3+) and not elimination of `DashboardCatalog.Load()` dual path (W1).

## Decision

1. **SSOT:** `src/DashSpec.Host/Catalog/dash.catalog.gdl` via `authoring/dashspec.gdlproj`.
2. **Generated artifact:** `src/DashSpec.Host/Generated/DashCatalog.g.cs` — committed, regen via MSBuild `BeforeCompile` or explicit emit target.
3. **Emit CLI:** `-t:GdlEmit` is a dispatcher alias (input **or** project mode). For `GdlEmitProject`, stamp invalidation runs **before** the incremental check when `GdlEmitForce=true` or when `Generated/*.g.cs` is missing — so deleted generated files and force regen no longer silently skip.
4. **Drift gate:** MSBuild target `GdlEmitVerify` (from `authoring-toolchain/build/Platform.Gdl.Emit.targets`) re-emits to `obj/.../gdl-emit-verify` and fails on byte mismatch.
5. **CI:** `DashSpec.GdlEmit.Tests/DashCatalogEmitDriftTests` builds `gdlc` then runs `dotnet msbuild -t:GdlEmitVerify` on `DashSpec.Host.csproj` (no Host C# compile required).

## Local workflow

```powershell
# Regen (when dash.catalog.gdl changes)
dotnet build src/DashSpec.Host/DashSpec.Host.csproj -c Release -p:GdlEmitForce=true

# Or explicit emit only (restores missing Generated/*.g.cs too)
dotnet msbuild src/DashSpec.Host/DashSpec.Host.csproj -c Release -t:GdlEmit

# Drift check (same as CI gate)
dotnet msbuild src/DashSpec.Host/DashSpec.Host.csproj -p:Configuration=Release -t:GdlEmitVerify

# Or via test suite
dotnet test tests/DashSpec.GdlEmit.Tests/DashSpec.GdlEmit.Tests.csproj -c Release --filter DashCatalogEmitDrift
```

**Prerequisite:** sibling checkout `../authoring-toolchain` (Host imports `Platform.Gdl.Emit.props` when present).

## Non-goals (this leaf)

- Remove `DashboardCatalog.Load()` runtime-parse path (W1).
- `DashSpec.Modeling.*` scaffold or grammar port (W3+).
- Deck / planet gdlproj expansion beyond catalog.

## Consequences

- PRs that edit `dash.catalog.gdl` without regen fail CI.
- Reviewers reject hand-maintained constants parallel to GDL emit (ADR-0051 anti-pattern).
