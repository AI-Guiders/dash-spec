# Changelog

Формат основан на [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Версии **0.x** — early preview; ломающие изменения DSL фиксируются в `design/DASHSPEC-ADR-*.md`.

## [Unreleased]

### Added

- **Filters pipe-table (ADR-0105)** — `filters { | id | bind | column | label | widget | ref | single | grain_filter | min | max | bind_scope | range | limit | }` inside `filters … end filters`; rows desugar into the same `FilterDefinition` IR through the shared semantics path (date-range normalization, widget vocabulary, top rules); values use the platform words `range`/`limit`; nested `filter` blocks stay valid (legacy, file-by-file migration). `BlockGrammar` gains the reusable `TableRowRows` member; `parseFiltersBlock` consolidates onto the shared container.
- **Table columns pipe-table (ADR-0104)** — `columns` as flat list or pipe table `| id | format | label | sorting |` inside `table`; desugars into `columns` / `column_formats` / `column_labels` / `order_by` (conflicts are parse errors). `TablePayload.HeaderTexts` + labeled table headers; column ids stay internal. First body form: pipe rows (contextual block grammar, GDL-table form).

### Changed

- **W10 — растворение `DashSpec.Core` (ADR-0048), завершено:** Runtime-кодеки, layout-механика, resolution/analysis, parsing, authoring, компиляторный контракт и диагностика разъехались по `DashSpec.Execution.{Runtime,Compilation,Core}`; stdlib переехала с парсерами в `Execution.Core`; `SpecLibrary` разрезан по I/O-шву (`SpecLibraryLoader` + реестр). **Проект `DashSpec.Core` удалён.** Проверено smoke-хостом (validate + живой рендер отчётов).

### Added

- **Plugin families (ADR-0032 / ADR-0033)** — `IDashSpecPlugin`, contributor registry, TOML bundles/tiers, `extensions { use … }`, extension blocks on cards, `GET /dev/capabilities`, viz dispatch via `CardVizComponentRegistry`, sample plugin `DashSpec.Plugin.Export`, template `dotnet new dashspec-extension`, [docs/PLUGINS.md](docs/PLUGINS.md).
- **Phrase templates and scopes (ADR-0034)** — SpecFlow-style plugin phrases in `on click { }`, `invoke`/`run` call args, `scope_builtin`, `InvokeHandlerEffect`.
- **Responsive layout + CSS modules** — split `app.css` into `css/parts/*`, wider content area, stack cards only ≤768px, taller diagram slots.
- **Heatmap guard** — `MatrixRenderLimits` (2500 cells / 80 axis); oversize message instead of DOM grid.
- **Matrix canvas renderer** — `render = "matrix-canvas"` viz plugin (default for matrix); one `<canvas>` + hit-test; `css-grid` kept as legacy.
- **Report scale (ADR-0030)** — `when` (card visibility / placeholder), `phase browse|detail`, `focus <phase>`; bar category click → `set … from y` when card has navigation `on click`.
- **Bar drill** — horizontal/vertical bar category click invokes `OnCategoryClick` → same navigation pipeline as heatmap (`set`, `focus`, `goto tab`, `drill to …`).
- **Action runtime** — `IDashSpecActionHandler`, `DashSpecActionDispatcher`, `csv_export` download from table/matrix/chart; parse-time lint for unknown action/interaction handlers.
- **Mandatory `scope` in `.dashlayout`** — `scope toolbar|tab|card` after `@layout <id>`; static check against include site. [ADR-0026](design/DASHSPEC-ADR-0026-layout-module-scope.md)

### Added

- **Bounded `on click` on cards** — `show below as list|plain|kv`, `set filter from x|y|value`, `goto tab`. [ADR-0028](design/DASHSPEC-ADR-0028-bounded-card-click-interactions.md)

### Proposed

- **Single declaration + layout by canonical id** — no `ref` / slot layer in new specs; [ADR-0027](design/DASHSPEC-ADR-0027-single-declaration-and-layout-ids.md)
- **Inspect (tooltip) split from diagram** — `presentation { inspect { tooltip { … } } }`; hover = inspect, click = behaviour; [ADR-0029](design/DASHSPEC-ADR-0029-inspect-tooltip-presentation-split.md)

### Changed

- **Breaking:** `.dashlayout` files without `scope` no longer parse

## [0.3.0] - 2026-07-02

### Added

- **Card `ref` и tab layout board** — компактная ASCII-сетка на вкладке (`[ Q W ] / [ E ]`). [ADR-0020](design/DASHSPEC-ADR-0020-card-ref-and-layout-board.md)
- **`.dashlayout` и `include layout`** — вынос board вкладки в отдельный файл. [ADR-0021](design/DASHSPEC-ADR-0021-dashlayout-include.md)
- **Filter `ref`, toolbar board и `include toolbar`** — многострочный toolbar через ту же bracket-модель; `.dashlayout` переиспользуется для toolbar. [ADR-0022](design/DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md)
- **`@runtime`** — каноническая директива bootstrap TOML (`@config` — deprecated alias). [ADR-0019](design/DASHSPEC-ADR-0019-runtime-directive.md)
- File includes: `.dashdiagram`, `.dashpresentation`, `.dashtransform`, `.dashpalette`, `.dashlayout`. [ADR-0017](design/DASHSPEC-ADR-0017-file-includes-and-stdlib.md)
- **`@tab` modules** и `tab … dashspec "module.dashspec"`. [ADR-0011](design/DASHSPEC-ADR-0011-tab-modules.md)

### Changed

- Парсер shell вынесен в `DashboardShellParser`; валидация — `DashboardValidator`
- Тесты Core разбиты по темам (120+ тестов)

## [0.2.0] - 2026-06

### Added

- Начальный импорт: DSL 0.2, layered parser, Blazor Server host
- Фильтры в Core (`filter`, `bind`, `QueryCompiler`) — [docs/FILTERS_RU.md](docs/FILTERS_RU.md)
- Коннектор-плагин **SqlServer** (`DashSpec.Connector.SqlServer`)
- Reference sample [`samples/demo/`](samples/demo/) (`demo-soak.dashspec`, `demo-analytics.dashspec`)
- ADR trail в `design/` (connectors, bind-only filters, presentation, tab modules, …)

[0.3.0]: https://github.com/AI-Guiders/dash-spec/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/AI-Guiders/dash-spec/releases/tag/v0.2.0
