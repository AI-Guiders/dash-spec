# DASHSPEC-ADR-0017: File includes and stdlib (PlantUML-style)

| | |
|---|---|
| **Status** | Accepted · v0.5 |
| **Date** | 2026-07-01 |
| **Relates to** | [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md), [ADR-0008](DASHSPEC-ADR-0008-viz-render-plugins.md), [ADR-0015](DASHSPEC-ADR-0015-dev-spec-resolve-dashboard-palette.md), [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) |

## Context

`@diagramlibrary` TOML + `use <card-preset>` улучшает DRY, но ломает authoring: effective diagram/datasource не видны в `.dashspec`, ошибки «магические» ([ADR-0015](DASHSPEC-ADR-0015-dev-spec-resolve-dashboard-palette.md)).

[PlantUML stdlib / preprocessing](https://plantuml.com/preprocessing): один язык, `!include file.puml`, `!include <stdlib/path>` — угловые скобки = встроенная библиотека.

## Decision

### Один DSL — несколько корней файлов

| Расширение | Корень | Содержимое |
|------------|--------|------------|
| `.dashspec` | `@dashboard` / `@tab` | `runtime`, `configuration`, `wiring`, `report` — [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) |
| `.dashinclude` | `@include` | `layout`, `toolbar`, `diagram` registry (file-level) |
| `.dashdiagram` | `@diagram <id>` | `!include`, `<kind> { }`, optional `presentation { }` / `series { }` — без inner `diagram` ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md)) |
| `.dashpresentation` | `@presentation <id>` | properties inline — **без** `presentation { }` |
| `.dashtransform` | `@transform <id>` | properties inline — **без** `transform series { }` |
| `.dashpalette` | `@palette <id>` | `const` + mappings — **без** `palette { }` |
| `.dashlayout` | `@layout <id>` | board rows `[ Q W ]` |
| `.dashcatalog` | `@catalog <id>` | `default`, `entry …` — flat, без `catalog { }` |

### File-level `.dashinclude` ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md))

```text
@include stakeholder_shell

layout "layouts/stakeholder-grid.dashlayout"
diagram "diagrams/stakeholder-peak-apps-heatmap.dashdiagram"
```

В `.dashspec` module:

```text
!include "imports/stakeholder.dashinclude"
!include "diagrams/stakeholder/*.dashdiagram"
```

Glob — см. [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) Layer 2: `*` в одном каталоге, sorted expand, explicit only (не auto-discovery).

На **card** — `diagram <id>` (modular) **или** inline `diagram <kind> { … }` (monolith). См. [ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md) authoring profiles.

### Include во fragment-файлах (`.dashdiagram`)

| Ссылка | Резолв |
|--------|--------|
| `"relative/path.dashdiagram"` | относительно каталога корневого `.dashspec` |
| `"<presentation/heatmap_tall>"` | `DashSpec.Core/stdlib/presentation/heatmap_tall.dashpresentation` |

Угловые скобки — как PlantUML `<aws/...>`: **stdlib продукта**, не пользовательский путь.

### Слои (без изменений ADR-0007)

| Слой | Где живёт |
|------|-----------|
| **diagram** | `.dashdiagram`; на card — `diagram <id>` |
| **presentation / transform** | `.dashdiagram` / stdlib via `!include` |
| **datasource + bind** | **card** в `report { }` |
| **palette** | `configuration.palette` + `wiring { use palette … }` |

`@diagramlibrary`, `use <card-preset>`, card-level `include diagram` — **удалены** ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md)).

### Stdlib

- Папка `src/DashSpec.Core/stdlib/` копируется в output Host/CLI.
- Базовые `presentation/*`, позже `diagram/*` рецепты.
- Список: `GET /dev/stdlib` (follow-up) / документация.

### Dev watcher

Перезагрузка при изменении `.dashspec`, `.dashinclude`, `.dashdiagram`, …; TOML из `runtime.manifest` — по необходимости.

## Non-goals v0.5

- `include_once` / zip `!import`

## Consequences

- LUS: `.dashinclude` + `diagram <id>` на card; `datasource`/`bind` в `report { }`.
- Core: `DashIncludeParser`, diagram registry в resolve; flat/card `include` удалён ([ADR-0024](DASHSPEC-ADR-0024-document-authoring-layers.md)).
