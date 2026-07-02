# DASHSPEC-ADR-0017: File includes and stdlib (PlantUML-style)

| | |
|---|---|
| **Status** | Accepted · v0.5 |
| **Date** | 2026-07-01 |
| **Relates to** | [ADR-0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md), [ADR-0008](DASHSPEC-ADR-0008-viz-render-plugins.md), [ADR-0015](DASHSPEC-ADR-0015-dev-spec-resolve-dashboard-palette.md) |

## Context

`@diagramlibrary` TOML + `use <card-preset>` улучшает DRY, но ломает authoring: effective diagram/datasource не видны в `.dashspec`, ошибки «магические» ([ADR-0015](DASHSPEC-ADR-0015-dev-spec-resolve-dashboard-palette.md)).

[PlantUML stdlib / preprocessing](https://plantuml.com/preprocessing): один язык, `!include file.puml`, `!include <stdlib/path>` — угловые скобки = встроенная библиотека.

## Decision

### Один DSL — несколько корней файлов

| Расширение | Корень | Содержимое |
|------------|--------|------------|
| `.dashspec` | `@dashboard` / `@tab` | dashboard, filters, cards |
| `.dashdiagram` | `@diagram <id>` | `diagram { }`, опционально `presentation`, `transform series` |
| `.dashpresentation` | `@presentation <id>` | `presentation { }` |
| `.dashtransform` | `@transform <id>` | `transform series { }` |
| `.dashpalette` | `@palette <id>` | `palette { }` (цвета серий, quoted keys для имён с пробелами) |
| `.dashlayout` | `@layout <id>` | bracket board `[ Q W ]` — см. [ADR-0021](DASHSPEC-ADR-0021-dashlayout-include.md) |

Парсер тот же, что inline-блоки в card.

### Include в card (и во fragment-файлах)

```text
card activity as "Activity" {
  include diagram "diagrams/activity-hour.dashdiagram"
  datasource view lus.v_hourly_activity
  bind activity_slot, app_name
}
```

```text
include presentation "<presentation/heatmap_tall>"
```

| Ссылка | Резолв |
|--------|--------|
| `"relative/path.dashdiagram"` | относительно каталога корневого `.dashspec` |
| `"<presentation/heatmap_tall>"` | `DashSpec.Core/stdlib/presentation/heatmap_tall.dashpresentation` |

Угловые скобки — как PlantUML `<aws/...>`: **stdlib продукта**, не пользовательский путь.

### Слои (без изменений ADR-0007)

| Слой | Где живёт |
|------|-----------|
| **diagram** | `.dashdiagram` или inline |
| **presentation / transform** | inline, include, или stdlib |
| **datasource + bind** | **всегда в card** в `.dashspec` (явная проводка данных) |
| **palette** | `@palette "palettes/*.dashpalette"` + `palette <id>` на dashboard/tab; `[palette.*]` TOML — **deprecated** |

`use <card-preset>` и `@diagramlibrary` **deprecated**; новый authoring — `include` + явный `datasource` / `@palette`.

### Stdlib

- Папка `src/DashSpec.Core/stdlib/` копируется в output Host/CLI.
- Базовые `presentation/*`, позже `diagram/*` рецепты.
- Список: `GET /dev/stdlib` (follow-up) / документация.

### Dev watcher

Перезагрузка при изменении `.dashspec`, `.dashdiagram`, `.dashpresentation`, `.dashtransform`, `.dashpalette`, `.dashlayout`, `.sql` в каталоге spec (TOML `@runtime` — по необходимости).

## Non-goals v0.5

- `include_once` / zip `!import`
- Полное удаление парсера `@diagramlibrary` / TOML presets (deprecated, но пока в Core)

## Consequences

- LUS: `docs/dashspec/diagrams/*.dashdiagram`, `palettes/*.dashpalette`; cards с явным `datasource`/`bind`.
- Core: `SpecIncludeResolver`, module parsers, тесты include + stdlib.
- `@diagramlibrary` / TOML presets — только для обратной совместимости; новые продукты — файловые модули.
