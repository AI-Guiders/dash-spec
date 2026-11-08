---
type: Index
title: "ADR index — dash-spec"
description: "Автоматический индекс ADR (126 записей, обновлён unknown). Генерируется scripts/gen-adr-index.py из frontmatter — не редактировать руками."
tags: [adr, index, dashspec]
---

# ADR index

Производное состояние (OKF): таблица ниже собирается скриптом из frontmatter
файлов ADR. Следующий номер — **0128**. Ручные правки будут перезаписаны:
`python scripts/gen-adr-index.py` (CI-проверка актуальности: `--check`).

| ADR | Title | Status |
|-----|-------|--------|
| [0001](DASHSPEC-ADR-0001-connectors-as-plugins.md) | Коннекторы как plugins | Accepted |
| [0002](DASHSPEC-ADR-0002-layout-and-presentation.md) | Layout и presentation в spec | Accepted |
| [0003](DASHSPEC-ADR-0003-diagram-kinds-registry.md) | Diagram kinds — registry вместо enum | Accepted |
| [0004](DASHSPEC-ADR-0004-diagram-column-as.md) | Column binding — `column as "Label"` | Accepted |
| [0005](DASHSPEC-ADR-0005-rich-text-creole-subset.md) | Rich text — Creole-subset (не PlantUML engine) | Accepted |
| [0006](DASHSPEC-ADR-0006-sql-datasource-and-sqldialect.md) | `@sqldialect` и семантика SQL-источников | Accepted |
| [0007](DASHSPEC-ADR-0007-presentation-transform-diagramlibrary.md) | `presentation`, `transform` и `@diagramlibrary` | Accepted |
| [0008](DASHSPEC-ADR-0008-viz-render-plugins.md) | Diagram library presets and viz render plugins | Accepted |
| [0009](DASHSPEC-ADR-0009-bind-only-filters.md) | `bind` — единственный список фильтров карточки | Accepted |
| [0010](DASHSPEC-ADR-0010-spec-ergonomics.md) | `on`, `toolbar`, `use card.*`, `bind dashboard` | Accepted |
| [0011](DASHSPEC-ADR-0011-tab-modules.md) | `@tab` modules and `tab … dashspec` | Accepted |
| [0012](DASHSPEC-ADR-0012-host-presentation-layering.md) | Host presentation layering | Accepted |
| [0013](DASHSPEC-ADR-0013-host-solid-ports-viz-registry.md) | Host SOLID ports and viz plugin registry | Accepted |
| [0014](DASHSPEC-ADR-0014-chart-series-colors.md) | Chart series colors in spec | Accepted |
| [0015](DASHSPEC-ADR-0015-dev-spec-resolve-dashboard-palette.md) | Dev spec resolve, dashboard palette | Accepted |
| [0016](DASHSPEC-ADR-0016-bar-reference-markers.md) | Bar reference markers (purchased seats / entitlement) | Accepted |
| [0017](DASHSPEC-ADR-0017-file-includes-and-stdlib.md) | File includes and stdlib (PlantUML-style) | Accepted |
| [0018](DASHSPEC-ADR-0018-sql-datasource-carriers.md) | `datasource sql` — query и file (без legacy) | Accepted |
| [0019](DASHSPEC-ADR-0019-runtime-directive.md) | `@runtime` — manifest вне DSL | Accepted |
| [0020](DASHSPEC-ADR-0020-card-ref-and-layout-board.md) | Card `ref` and tab layout board | Accepted |
| [0021](DASHSPEC-ADR-0021-dashlayout-include.md) | `.dashlayout` and `include layout` | Accepted |
| [0022](DASHSPEC-ADR-0022-toolbar-ref-and-layout-board.md) | Filter `ref` and toolbar layout board | Accepted |
| [0023](DASHSPEC-ADR-0023-dashcatalog.md) | `.dashcatalog` and report catalog | Accepted |
| [0024](DASHSPEC-ADR-0024-document-authoring-layers.md) | Document grammar — blocks, `.dashinclude`, `report` | Accepted |
| [0025](DASHSPEC-ADR-0025-card-interior-layout-board.md) | Card interior layout board | Accepted |
| [0026](DASHSPEC-ADR-0026-layout-module-scope.md) | mandatory `scope` in `.dashlayout` | Accepted |
| [0027](DASHSPEC-ADR-0027-single-declaration-and-layout-ids.md) | Single declaration and layout by canonical id | Proposed |
| [0028](DASHSPEC-ADR-0028-bounded-card-click-interactions.md) | Bounded `on click` interactions | Accepted (v1 implemented; amended by ADR-0031) |
| [0029](DASHSPEC-ADR-0029-inspect-tooltip-presentation-split.md) | Tooltip as entity (content only) | Accepted |
| [0030](DASHSPEC-ADR-0030-report-scale-pages-gates-and-suites.md) | Report scale — `page`, `gate`, `phase`, catalog `group` | Accepted (partial — P0–P2 implemented; P3 pending) |
| [0031](DASHSPEC-ADR-0031-display-vocabulary-no-as.md) | Display vocabulary — remove `as` | Proposed |
| [0032](DASHSPEC-ADR-0032-extension-blocks-and-plugins.md) | Extension blocks and plugins | Accepted |
| [0033](DASHSPEC-ADR-0033-plugin-families-and-microkernel-host.md) | Plugin families and microkernel host | Accepted |
| [0034](DASHSPEC-ADR-0034-phrase-templates-and-scopes.md) | Phrase templates and document scopes | Accepted |
| [0035](DASHSPEC-ADR-0035-chrome-and-filter-widget-families.md) | Card chrome and filter widget families | Accepted |
| [0036](DASHSPEC-ADR-0036-end-blocks-page-toolbar.md) | End blocks, page toolbar, filter derive, presentation scale | Accepted |
| [0037](DASHSPEC-ADR-0037-filter-scopes-and-toolbar-grouping.md) | Фильтры — bind, show, видимость и раскладка | Accepted |
| [0038](DASHSPEC-ADR-0038-structured-card-and-report-composition.md) | Структурированный card и report как composition | Proposed |
| [0039](DASHSPEC-ADR-0039-chart-chrome-merge.md) | Chart chrome merge (`chrome { use … }`) | Accepted |
| [0040](DASHSPEC-ADR-0040-chart-recipes-coords-channels.md) | Chart recipes — coords / channels / marks (narrow) | Accepted |
| [0041](DASHSPEC-ADR-0041-git-catalog-push-sync.md) | Git catalog sync on push (webhook), poll as safety net | Accepted |
| [0042](DASHSPEC-ADR-0042-host-control-center-witdb.md) | Host Control Center + WitDB settings SSOT | Accepted |
| [0043](DASHSPEC-ADR-0043-filter-command-palette.md) | Filter command palette (CommandPlane adapter) | Accepted |
| [0044](DASHSPEC-ADR-0044-date-filter-value-constructor.md) | Date filter value constructor (CCL) | Accepted |
| [0045](DASHSPEC-ADR-0045-date-filter-grain-constructors.md) | Date filter grain constructors | Accepted |
| [0046](DASHSPEC-ADR-0046-ccl-locale-typed-value-input.md) | CCL locale typed value input (DashSpec adapter) | Accepted |
| [0047](DASHSPEC-ADR-0047-platform-surfaces-viewer-split.md) | DashSpec Platform vs surfaces (Web Host = viewer) | Accepted |
| [0048](DASHSPEC-ADR-0048-model-migration-map.md) | Карта миграции модели (C -> только F#) | Working document |
| [0048](DASHSPEC-ADR-0048-modeling-execution-split-fsharp.md) | Modeling vs Execution — planet DSL split (F# parse) | Accepted |
| [0049](DASHSPEC-ADR-0049-git-catalog.md) | Git catalog source | Accepted |
| [0050](DASHSPEC-ADR-0050-category-chart-color-column.md) | Category chart color — column binding | Accepted |
| [0051](DASHSPEC-ADR-0051-fullscreen-detail-payload.md) | Fullscreen detail payload (без Other) | Accepted |
| [0051](DASHSPEC-ADR-0051-language-affinity-modeling-execution.md) | Language affinity — Modeling (F#) vs Execution (C#) | Accepted |
| [0052](DASHSPEC-ADR-0052-catalog-emit-drift-gate.md) | Catalog emit drift gate (W0) | Accepted |
| [0053](DASHSPEC-ADR-0053-editorconfig-and-document-formatter.md) | EditorConfig floor + DashSpec document formatter (MLP) | Accepted |
| [0054](DASHSPEC-ADR-0054-dashhost-module.md) | `.dashhost` — planet-content shell Host | Accepted |
| [0055](DASHSPEC-ADR-0055-gantt-timeline-renderer.md) | Gantt timeline renderer (`gantt-timeline`) | Accepted |
| [0056](DASHSPEC-ADR-0056-layout-card-groups.md) | Layout card groups (GroupBox) | Accepted |
| [0057](DASHSPEC-ADR-0057-resolution-registry.md) | Resolution registry — единая точка «кто побеждает» | Accepted |
| [0058](DASHSPEC-ADR-0058-display-bindings.md) | Display bindings (`bind display`) | Accepted |
| [0059](DASHSPEC-ADR-0059-vertical-viz-plugins.md) | Vertical viz render plugins | Accepted |
| [0060](DASHSPEC-ADR-0060-vertical-filter-plugins.md) | Vertical filter widget plugins | Accepted |
| [0061](DASHSPEC-ADR-0061-layout-board-weights-and-filter-place.md) | Layout board weights and filter `place` | Accepted |
| [0062](DASHSPEC-ADR-0062-viz-axis-presentation.md) | Viz axis presentation (diagram-agnostic) | Accepted |
| [0063](DASHSPEC-ADR-0063-layout-board-nest.md) | Layout board nest (cell as grid) | Accepted |
| [0064](DASHSPEC-ADR-0064-cell-drill-tabular-payload.md) | Cell drill via table + tabular payload sources | Accepted (pilot; structured carrier — Proposed) |
| [0065](DASHSPEC-ADR-0065-card-interior-multi-slot.md) | Card interior multi-slot (no legacy host stack) | Accepted |
| [0066](DASHSPEC-ADR-0066-card-refresh-scopes.md) | Host refresh scopes (implementation of ADR-0028) | Accepted |
| [0067](DASHSPEC-ADR-0067-card-and-slot-fold-chrome.md) | Card fold chrome (BI viewport) | Accepted (v1: card body fold) |
| [0068](DASHSPEC-ADR-0068-diagram-designer-discoverability.md) | Diagram Designer — discoverability через VIEW + diagram (не question entity) | Proposed |
| [0069](DASHSPEC-ADR-0069-report-time-basis-and-work-calendar.md) | Report time basis (calendar vs working) and work calendar v1 | Accepted |
| [0070](DASHSPEC-ADR-0070-table-column-formats-block.md) | Table column formats — `formats` block | Accepted |
| [0071](DASHSPEC-ADR-0071-block-and-member-grammar.md) | Block and member grammar (parse spine) | Accepted |
| [0072](DASHSPEC-ADR-0072-unified-layout-slot-plane.md) | Unified layout slot plane (host grid = card grid) | Accepted |
| [0073](DASHSPEC-ADR-0073-author-navigation-surface.md) | Author navigation surface — `report` → `section` → `page` → `card` → `view` → `diagram` | Accepted (grammar + composer: planned; Host: align incrementally) |
| [0074](DASHSPEC-ADR-0074-host-shell-composed-view.md) | Host as shell — composed view, no UI defaults | Accepted (composer contract: normative; Host refactor: incremental) |
| [0076](DASHSPEC-ADR-0076-architecture-build-guards.md) | Architecture build guards — analyzers + source rules | Accepted |
| [0077](DASHSPEC-ADR-0077-host-ui-copy-ssot.md) | Host UI copy — SSOT and build-time freeze | Accepted |
| [0078](DASHSPEC-ADR-0078-dashflow-data-plane.md) | Dashflow — data plane (sources, transformers, card inputs) | Accepted (concept + IR; parser / runtime phased) |
| [0079](DASHSPEC-ADR-0079-dashflow-type-system.md) | Dashflow type system — strict static, value-only | Accepted (normative for dashflow; phased implementation) |
| [0080](DASHSPEC-ADR-0080-dataflow-transform-plugins.md) | Dataflow transforms as plugins | Accepted (concept; loader phased) |
| [0081](DASHSPEC-ADR-0081-type-plugins.md) | Semantic types as plugins | Accepted (concept; loader phased) |
| [0082](DASHSPEC-ADR-0082-dashspec-sdk.md) | DashSpec SDK (plugin author surface) | Accepted (packaging phased) |
| [0083](DASHSPEC-ADR-0083-dataflow-engine-cockpit-transport.md) | Dashflow — federation **DataFlow** (separate from Cockpit) | Accepted (architecture; implementation phased) |
| [0084](DASHSPEC-ADR-0084-matrix-axis-scroll-sticky.md) | Matrix heatmap — sticky axis scroll | Accepted |
| [0085](DASHSPEC-ADR-0085-data-acquisition-architecture-analyzers.md) | Data Acquisition vs Data Flow — boundaries and DSPEC analyzers | Accepted |
| [0086](DASHSPEC-ADR-0086-surfaces-as-plugins-bff.md) | Surfaces as plugins — viewer-agnostic platform + BFF | Accepted |
| [0087](DASHSPEC-ADR-0087-typed-row-batch-analyzers.md) | Typed row batch (`rows R`) — wire model and DSPEC031–034 | Accepted |
| [0088](DASHSPEC-ADR-0088-flow-composition-subprocess.md) | Composable flows (BPMN-style subprocess) | Accepted (concept + IR contract; parser phased) |
| [0089](DASHSPEC-ADR-0089-document-modules-using-import.md) | Document modules — `using` / `import` instead of preprocessor `!include` | Accepted (Phase 1 in code; Phase 2 project membership specified) |
| [0090](DASHSPEC-ADR-0090-card-interior-flow.md) | Card wiring (links only) | Accepted |
| [0090](DASHSPEC-ADR-0090-report-culture-defaults.md) | Report culture as locale SSOT | Accepted |
| [0091](DASHSPEC-ADR-0091-unified-graph-flow-and-routing.md) | Unified Graph — one wire syntax | Accepted |
| [0092](DASHSPEC-ADR-0092-report-scope-routing-and-events.md) | Report-scope routing and events | Accepted |
| [0093](DASHSPEC-ADR-0093-qualified-flow-graph-kinds.md) | Qualified flow graph kinds | Accepted |
| [0094](DASHSPEC-ADR-0094-report-page-data-flow.md) | Report/page data flow | Accepted |
| [0095](DASHSPEC-ADR-0095-dashboard-shell-wiring-pipeline.md) | Dashboard shell wiring pipeline (SSOT) | Accepted |
| [0096](DASHSPEC-ADR-0096-placement-flow.md) | Placement flow (filter surfaces) | Accepted |
| [0097](DASHSPEC-ADR-0097-document-compile-pipeline.md) | Document compile pipeline (phase registry) | Accepted |
| [0098](DASHSPEC-ADR-0098-compiler-import-namespaces.md) | Compiler modules — `namespace` and `import` | Accepted |
| [0099](DASHSPEC-ADR-0099-B3-viewer-migration-checklist.md) | viewer migration checklist (Host -> Surface.Blazor) | Accepted |
| [0099](DASHSPEC-ADR-0099-surface-blazor-contract-testing.md) | Surface.Blazor — full separation and contract testing | Accepted |
| [0100](DASHSPEC-ADR-0100-diagram-kind-catalog.md) | Diagram kind — вертикальная плагинная способность и каталог-композиция (SSOT) | Accepted |
| [0101](DASHSPEC-ADR-0101-single-unit-file-type.md) | Single compilation-unit file type (`.ds`) and `module <kind> <id>` envelope | Accepted |
| [0102](DASHSPEC-ADR-0102-one-language.md) | One language | Accepted |
| [0103](DASHSPEC-ADR-0103-surfaces-read-ir-must-not-parse.md) | Surfaces read the F# IR; they must not parse | Accepted |
| [0104](DASHSPEC-ADR-0104-table-columns-pipe-table.md) | Table columns pipe-table — `id | format | label | sorting` | Accepted |
| [0105](DASHSPEC-ADR-0105-filters-pipe-table.md) | Filters pipe-table — `filters { | id | bind | … | }` | Accepted |
| [0106](DASHSPEC-ADR-0106-multi-form-constructs.md) | Multi-form constructs — `Construct.declare` | Accepted |
| [0107](DASHSPEC-ADR-0107-sourced-clicks.md) | Sourced clicks — `on <source> click` + the click table | Accepted |
| [0108](DASHSPEC-ADR-0108-eventsource-handler.md) | EventSource + Handler — the interaction subsystem | Accepted (direction) |
| [0109](DASHSPEC-ADR-0109-filter-wiring-ssot.md) | SSOT проводки фильтров клика | Accepted |
| [0110](DASHSPEC-ADR-0110-host-shell-slot-plane.md) | Оболочка хоста как слотовая плоскость — nav-плагин и открытые контент-слоты | Accepted |
| [0111](DASHSPEC-ADR-0111-assembled-slot-plane.md) | Собранная слотовая плоскость — SSOT визуальной композиции | Accepted |
| [0112](DASHSPEC-ADR-0112-render-stack-and-theme-token.md) | Рендер-стек — выбор визуального фреймворка и тема как токен | Proposed |
| [0113](DASHSPEC-ADR-0113-property-based-testing.md) | Property-based testing — парсеры и грамматики как исполняемые законы | Proposed |
| [0114](DASHSPEC-ADR-0114-switch-slot-payloads.md) | Switch slot — multi-payload diagram slot | Accepted |
| [0115](DASHSPEC-ADR-0115-radzen-table-grid-extras.md) | Radzen-рендер — паритет фич с self-made | Accepted |
| [0116](DASHSPEC-ADR-0116-radzen-heatmap-feedback.md) | Radzen-тепловая карта — паритет и размеры от контента | Accepted |
| [0117](DASHSPEC-ADR-0117-dsc-cli-composition-root.md) | dsc — CLI composition root; компилятор — библиотека за контрактом | Proposed |
| [0118](DASHSPEC-ADR-0118-filter-widget-role-contract.md) | Контракт ролей фильтров — skin уходит стеку, реализации становятся замещаемыми | Accepted |
| [0122](DASHSPEC-ADR-0122-default-flow-wiring.md) | Default flow wiring — компилятор синтезирует линейные дуги карточек | Accepted |
| [0123](DASHSPEC-ADR-0123-runtime-grouping.md) | Группировка и порядок — runtime-трансформеры payload→payload; сводка остаётся database group_by | Proposed |
| [0124](DASHSPEC-ADR-0124-row-types-compiled-by-dsc.md) | Row types компилируются в CLR-типы (dsc build types, Roslyn) — доменный объект доезжает до рендера | Proposed |
| [0125](DASHSPEC-ADR-0125-dashspec-managed-views.md) | DashSpec-managed views — публикация создаёт view в БД из флоу-графа (dsc build views) | Proposed |
| [0127](DASHSPEC-ADR-0127-transform-port-type-inference.md) | Transform port type inference — компилятор выводит типы портов трансформеров из входных рёбер и алгебры шагов | Proposed |
