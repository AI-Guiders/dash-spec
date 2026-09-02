# DASHSPEC-ADR-0049: GDL Report Edition — ship DashSpec as federation quarries

| | |
|---|---|
| **Status** | Accepted |
| **Date** | 2026-09-02 |
| **Tags** | #dashspec #gdl #report-edition #authoring #federation #modeling #execution |
| **Relates to** | [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) · [ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) · [ADR-0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md) · [GUIDERS-ADR-0059](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0059-gdl-hyperlane.md) · [GUIDERS-ADR-0048](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0048-authoring-quarry-family.md) · [GUIDERS-FSHARP-ADR-0002](https://github.com/AI-Guiders/guiders-fsharp/blob/main/docs/adr/GUIDERS-FSHARP-ADR-0002-model-guild-fsharp-ownership.md) · [CDP-ADR-0208](https://github.com/AI-Guiders/cdp-mcp/blob/main/docs/adr/CDP-ADR-0208-language-code-ir-fsharp-fcs.md) |

## Context

[DASHSPEC-ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) split DashSpec into **Modeling** (parse/IR) and **Execution** (runtime/session) and treated `.dashspec` as a **planet-sovereign** grammar outside federation GDL quarries ([GUIDERS-ADR-0059](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0059-gdl-hyperlane.md) §5).

That boundary fit **third-party planets** (URSA content, IncomeCascade specs) — they ship report *content*, not the language.

**DashSpec platform is fully ours** (AI-Guiders product, not an external sovereign DSL). There is no reason to keep report grammar outside GDL while `catalog`, `deck`, and `cockpit.logic` live in Authoring Guild.

**Decision:** ship report authoring as **GDL Report Edition** — a named **edition** of GDL (quarry family + tooling + execution stack), same product posture as deck/catalog quarries, not a parallel «DashSpec DSL» silo.

## Decision

### 1. Product identity

| Name | Meaning |
|------|---------|
| **GDL** | Federation declare-time language ([0059](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0059-gdl-hyperlane.md)) |
| **Report Edition** | GDL quarry family for BI/report/dashboard authoring — grammar, IR, validation, conformance |
| **DashSpec Platform** | Execution + surfaces ([ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md)): resolve, bind, connectors, session, Host, Studio |
| **Planet content** | Customer `.gdl` / legacy `.dashspec` *instances* in product repos (URSA, …) — not grammar owners |

**One-liner:** *GDL Report Edition = what the report declares. DashSpec Platform = how it runs.*

### 2. GDL intent stack (extended)

```text
*.catalog.gdl           — what you can do
*.deck.gdl              — where you look (zones, topology)
*.cockpit.logic.gdl     — when things light up
*.display.gdl           — where it lands on hardware
── Report Edition ──────────────────────────────────────
*.report.gdl            — dashboard / tab module (report meaning)
*.diagram.report.gdl    — diagram fragment
*.layout.report.gdl     — layout board
*.palette.report.gdl    — color / const palette
*.presentation.report.gdl
*.transform.report.gdl
*.include.report.gdl    — file-level include registry
*.catalog.report.gdl    — dashcatalog (report-local catalog)
*.tooltip.report.gdl
```

Report Edition quarries use the **same GDL lexical core** as federation ([0059](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0059-gdl-hyperlane.md) §3): `end keyword`, `#`, `import`, tables — no `{ }` blocks ([ADR-0036](DASHSPEC-ADR-0036-end-blocks-page-toolbar.md)).

### 3. Authoring Guild ownership

Report Edition grammar is **Authoring Guild SSOT** — not a planet fork:

```text
Platform.Modeling.Gdl.Report.*     F#   parse, IR, validation, conformance (guiders-fsharp)
DashSpec.Execution.*               C#   resolve, bind, session, connectors (dash-spec repo)
```

| Layer | Repo | Packages |
|-------|------|----------|
| **Modeling** | `guiders-fsharp` | `AIGuiders.Platform.Modeling.Gdl.Report.Core`, `.Parse.*`, `.Validation` |
| **Execution** | `dash-spec` | `DashSpec.Execution.Core`, `.Runtime`, `.Compilation`, connectors, Host |

**Dependency rule:** `DashSpec.Execution.*` → `Platform.Modeling.Gdl.Report.*` (same as [GUIDERS-FSHARP-ADR-0002](https://github.com/AI-Guiders/guiders-fsharp/blob/main/docs/adr/GUIDERS-FSHARP-ADR-0002-model-guild-fsharp-ownership.md)).

`DashSpec.Modeling.*` from [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) is a **transitional planet prefix** until packages land in `guiders-fsharp` under `Platform.Modeling.Gdl.Report.*`.

### 4. File naming migration (transitional)

| Legacy (v0) | Canonical GDL (target) | Notes |
|-------------|------------------------|-------|
| `foo.dashspec` | `foo.report.gdl` | `@dashboard` / `@tab` roots unchanged semantically |
| `bar.dashdiagram` | `bar.diagram.report.gdl` | |
| `grid.dashlayout` | `grid.layout.report.gdl` | |
| `p.dashpalette` | `p.palette.report.gdl` | |
| `*.dashinclude` | `*.include.report.gdl` | |
| `*.dashcatalog` | `*.catalog.report.gdl` | distinct from federation `*.catalog.gdl` — quarry token is `catalog.report` |

**Transitional:** parsers accept **both** suffixes during migration; emit/conformance targets canonical `*.{quarry}.gdl`. No bare `.dashspec` alias after cutover (same rule as bare `.deck` in [0059](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0059-gdl-hyperlane.md)).

Project manifest: `*.gdlproj` ([0051](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0051-authoring-project-abstraction.md)) — report workspace entries reference `*.report.gdl` roots.

### 5. CDP / IDE (informative)

Report Edition quarries register in **Language Resolver Center** as `CdpLanguages.Gdl` extensions (same backend family as `deck` / `catalog`) — [CDP-ADR-0208](https://github.com/AI-Guiders/cdp-mcp/blob/main/docs/adr/CDP-ADR-0208-language-code-ir-fsharp-fcs.md).

```text
foo.report.gdl  →  GdlBackend  →  Platform.Modeling.Gdl.Report.Parse
```

Planets with report content get diagnostics / outline / goto **out of the box** — no DashSpec-specific IDE fork.

### 6. What stays where

| Owner | Owns |
|-------|------|
| **Authoring Guild** (`Platform.Modeling.Gdl.Report.*`) | Report Edition grammar, IR, conformance vectors |
| **DashSpec Platform** (`DashSpec.Execution.*`, Host) | BI engine, connectors, session, git catalog, surfaces |
| **Studio planet** (`dash-spec-studio`) | WPF chrome, Data Lab, deck integration — Execution consumer |
| **Content planets** (URSA, …) | SQL, ETL, warm views, report *instances* — reference Report Edition packages, never fork grammar |

### 7. Relationship to deck quarry

- **`*.deck.gdl`** — attention topology, zones, where Report Preview mounts ([0058](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0058-presentation-topology-ir.md)).
- **`*.report.gdl`** — cards, filters, data bindings, report composition.

Studio pairs `report-author.deck.gdl` + `analytics.report.gdl` — complementary quarries, not merged files.

## Migration phases

```text
R0  ADR-0049 + federation quarry registry amend (GUIDERS-ADR-0059 §5 — Report Edition row)
R1  Platform.Modeling.Gdl.Report.Core scaffold (guiders-fsharp)
R2  Port smallest quarry (.layout.report.gdl) — proves GDL naming + BlockReader reuse
R3  DashSpec.Execution.* split ([ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) M0–M3) referencing Report Modeling packages
R4  .report.gdl (@dashboard / @card) — largest surface
R5  Dual-suffix parsers (legacy .dashspec + canonical .report.gdl)
R6  Conformance vectors under docs/conformance/authoring/report/
R7  Deprecate .dash* extensions; CDP GdlBackend report quarry registration
```

## Consequences

- DashSpec grammar **joins GDL** as Report Edition — same tooling, packaging, and CDP posture as catalog/deck.
- [ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) Modeling packages **graduate** to `Platform.Modeling.Gdl.Report.*`; dash-spec repo focuses on Execution + surfaces.
- Content planets stay thin: ship report files + SQL, not parsers.
- Federation ADR [0059](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0059-gdl-hyperlane.md) §5 «planet sovereign .dashspec» is **superseded** for platform-owned grammar (content planets remain consumers only).

## Non-goals (R0–R4)

- Forcing URSA/IncomeCascade file renames before dual-suffix window closes.
- Merging `*.report.gdl` and `*.deck.gdl` into one file.
- Moving DashSpec.Host connectors into guiders-fsharp.

## Federation follow-up

Amend [GUIDERS-ADR-0059](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0059-gdl-hyperlane.md) quarry registry + intent stack with Report Edition rows; update [gdl-hyperlane-signage-v1](https://github.com/AI-Guiders/kb/blob/main/knowledge/work/projects/aiguiders-open/guiders-federation/gdl-hyperlane-signage-v1.md) signage.

## References

- [DASHSPEC-ADR-0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) — Modeling/Execution split (transitional packages)
- [GUIDERS-ADR-0059](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0059-gdl-hyperlane.md) — GDL hyperlane + quarry registry
- [GUIDERS-ADR-0055](https://github.com/AI-Guiders/guiders-platform/blob/main/docs/adr/GUIDERS-ADR-0055-surface-wpf-guild-deck-authoring.md) — deck + Report Preview pairing
