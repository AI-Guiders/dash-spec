# Architecture Decision Records — dash-spec

ADR хранятся в `design/` (префикс `DASHSPEC-ADR-`). Полный индекс ниже; новый ADR — `DASHSPEC-ADR-NNNN-short-title.md`, следующий номер — 0049.

## Индекс

| ADR | Title | Status |
|-----|-------|--------|
| [0001](DASHSPEC-ADR-0001-connectors-as-plugins.md) | Коннекторы как plugins | Accepted · v0.2 |
| [0002](DASHSPEC-ADR-0002-layout-and-presentation.md) | Layout и presentation в spec | Accepted |
| [0003](DASHSPEC-ADR-0003-diagram-kinds-registry.md) | Diagram kinds — registry вместо enum | Accepted |
| [0004](DASHSPEC-ADR-0004-diagram-column-as.md) | Column binding — `column as "Label"` | Accepted |
| [0005](DASHSPEC-ADR-0005-rich-text-creole-subset.md) | Rich text — Creole-subset | Accepted |
| [0006](DASHSPEC-ADR-0006-sql-datasource-and-sqldialect.md) | `@sqldialect` и семантика SQL-источников | Accepted · v0.3 |
| [0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md) | `presentation`, `transform`, `@diagramlibrary` | Accepted · v0.4 |
| [0008](DASHSPEC-ADR-0008-viz-render-plugins.md) | Diagram library presets and viz render plugins | Accepted (presets/registry implemented; external DLL proposed) |
| [0009](DASHSPEC-ADR-0009-bind-only-filters.md) | `bind` — единственный список фильтров карточки | Accepted |
| [0010](DASHSPEC-ADR-0010-spec-ergonomics.md) | `on`, `toolbar`, `use card.*`, `bind dashboard` | Accepted |
| [0011](DASHSPEC-ADR-0011-tab-modules.md) | `@tab` modules and `tab … dashspec` | Accepted |
| [0012](DASHSPEC-ADR-0012-host-presentation-layering.md) | Host presentation layering | Accepted |
| [0013](DASHSPEC-ADR-0013-host-solid-ports-viz-registry.md) | Host SOLID ports and viz plugin registry | Accepted |
| [0014](DASHSPEC-ADR-0014-chart-series-colors.md) | Chart series colors in spec | Accepted |
| [0015](DASHSPEC-ADR-0015-dev-spec-resolve-dashboard-palette.md) | Dev spec resolve, dashboard palette | Accepted |
| [0016](DASHSPEC-ADR-0016-bar-reference-markers.md) | Bar reference markers | Accepted |
| [0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md) | File includes and stdlib (PlantUML-style) | Accepted · v0.5 |
| [0018](DASHSPEC-ADR-0018-sql-datasource-carriers.md) | `datasource sql` — query и file | Accepted · v0.6 |
| [0019](DASHSPEC-ADR-0019-runtime-directive.md) | `@runtime` — manifest вне DSL | Accepted · v0.6 |
| [0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md) | Card `ref` and tab layout board | Accepted |
| [0021](DASHSPEC-ADR-0021-dashlayout-include.md) | `.dashlayout` and `include layout` | Accepted |
| [0022](DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md) | Filter `ref` and toolbar layout board | Accepted |
| [0023](DASHSPEC-ADR-0023-dashcatalog.md) | `.dashcatalog` and report catalog | Accepted |
| [0024](DASHSPEC-ADR-0024-document-authoring-layers.md) | Document grammar — blocks, `.dashinclude`, `report` | Accepted |
| [0025](DASHSPEC-ADR-0025-card-interior-layout-board.md) | Card interior layout board | Accepted |
| [0026](DASHSPEC-ADR-0026-layout-module-scope.md) | mandatory `scope` in `.dashlayout` | Accepted |
| [0027](DASHSPEC-ADR-0027-single-declaration-and-layout-ids.md) | Single declaration and layout by canonical id | Proposed |
| [0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md) | Bounded `on click` interactions | Accepted (v1 implemented; amended ADR-0031) |
| [0029](DASHSPEC-ADR-0029-inspect-tooltip-presentation-split.md) | Tooltip as entity | Accepted |
| [0030](DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md) | Report scale — `page`, `gate`, `phase`, `group` | Accepted (partial — P0–P2 implemented; P3 pending) |
| [0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md) | Display vocabulary — remove `as` | Proposed |
| [0032](DASHSPEC-ADR-0032-extension-blocks-and-plugins.md) | Extension blocks and plugins | Accepted |
| [0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md) | Plugin families and microkernel host | Accepted |
| [0034](DASHSPEC-ADR-0034-phrase-templates-and-scopes.md) | Phrase templates and document scopes | Accepted |
| [0035](DASHSPEC-ADR-0035-chrome-and-filter-widget-families.md) | Card chrome and filter widget families | Accepted |
| [0036](DASHSPEC-ADR-0036-end-blocks-page-toolbar.md) | End blocks, page toolbar, filter derive | Accepted |
| [0037](DASHSPEC-ADR-0037-filter-scopes-and-toolbar-grouping.md) | Фильтры — bind, show, видимость и раскладка | Accepted |
| [0038](DASHSPEC-ADR-0038-structured-card-and-report-composition.md) | Структурированный card и report как composition | Proposed |
| [0039](DASHSPEC-ADR-0039-chart-chrome-merge.md) | Chart chrome merge | Accepted |
| [0040](DASHSPEC-ADR-0040-chart-recipes-coords-channels.md) | Chart recipes — coords / channels / marks | Accepted |
| [0041](DASHSPEC-ADR-0041-git-catalog-push-sync.md) | Git catalog sync on push (webhook) | Accepted |
| [0042](DASHSPEC-ADR-0042-host-control-center-witdb.md) | Host Control Center + WitDB settings SSOT | Accepted |
| [0043](DASHSPEC-ADR-0043-filter-command-palette.md) | Filter command palette | Accepted |
| [0044](DASHSPEC-ADR-0044-date-filter-value-constructor.md) | Date filter value constructor (CCL) | Accepted |
| [0045](DASHSPEC-ADR-0045-date-filter-grain-constructors.md) | Date filter grain constructors | Accepted |
| [0046](DASHSPEC-ADR-0046-ccl-locale-typed-value-input.md) | CCL locale typed value input | Accepted |
| [0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) | DashSpec Platform vs surfaces | Proposed |
| [0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) | Modeling vs Execution — planet DSL split (F# parse) | Accepted |
| [0049](DASHSPEC-ADR-0049-git-catalog.md) | Git catalog source | Accepted |