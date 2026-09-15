# DASHSPEC-II3: Presentation package scaffold

| | |
|---|---|
| **Status** | Active |
| **Date** | 2026-09-14 |
| **Phase** | II3 ([ADR-0051](DASHSPEC-ADR-0051-language-affinity-modeling-execution.md) §4b) |
| **Relates to** | [ADR-0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) §6 · [STUDIO-WAVE-A-CDP-HANDOFF](https://github.com/AI-Guiders/dash-spec-studio/blob/main/design/STUDIO-WAVE-A-CDP-HANDOFF.md) Track B |

## Goal

Create `DashSpec.Presentation` as the shared UI home for Host + Studio before component extract. Studio must not reference `DashSpec.Host`.

## Shipped (this slice)

| Artifact | Role |
|----------|------|
| `src/DashSpec.Presentation/` | net10.0 Razor Class Library shell (`PresentationPlaceholder.razor`) |
| `DashSpec.slnx` | project wired |
| `IReportPreviewSession` | Abstractions port stub for Report Preview + parity fingerprint |
| Package README | consumer map + ADR pointers |
| `RichTextView.razor` | Creole-subset inline markup ([ADR-0005](DASHSPEC-ADR-0005-rich-text-creole-subset.md)); extracted from Host |
| `DashSpec.Host` → `DashSpec.Presentation` | `ProjectReference`; card titles + heatmap/matrix axis/legend labels use shared component |

## Deferred (follow-up slices)

| Item | Why deferred |
|------|--------------|
| `IReportSession` full port | `IDashboardSession` still Host-local; move with Execution.Runtime session extract (II1) |
| Chart/card/filter extract | Requires Host slimming + Studio v0 wiring (Phase III) |
| Studio project reference | Track A (gdlc/deck) merges before Presentation refs |
| WebView2 → Host URL | Explicit anti-pattern per ADR-0047 |

## Dependency rule

```text
DashSpec.Presentation  →  DashSpec.Abstractions, DashSpec.Execution.Runtime (CreoleSubset)
DashSpec.Host            →  DashSpec.Presentation
dash-spec-studio         →  DashSpec.Presentation + Execution.* (Phase III1)
```

Presentation does **not** reference Host.

## Acceptance gate (Phase III1)

- Studio Report Preview renders via shared Presentation + Execution session
- Session parity test: same `.dashspec` → same `GetPayloadFingerprintAsync()` in Host and Studio
