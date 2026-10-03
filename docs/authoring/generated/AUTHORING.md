# DashSpec — справочник авторинга

> Сгенерировано из XML-doc (`AuthoringCatalog` + парсеры). Не редактировать вручную.
> Команда: `dotnet run --project src/DashSpec.DocGen`

# Обзор DashSpec
            
             DashSpec — DSL для дашбордов: shell (`.dashspec`), модули диаграмм (`.dashdiagram`),
             chrome-пресеты (`.dashpresentation`), layout, palette, catalog.
            
             Источник правды — парсер `DashSpec.Core` (Host и LSP используют один и тот же код).
            
             ## Типы файлов
            
             | Расширение | Назначение |
             |------------|------------|
             | `.dashspec` | dashboard shell или tab module |
             | `.dashdiagram` | `@diagram` + kind block (`heatmap`, `bar`, …) |
             | `.dashpresentation` | `@presentation` chrome preset |
             | `.dashlayout` | toolbar / card grid |
             | `.dashpalette` | цветовая палитра |
             | `.dashcatalog` | каталог entry → dashspec |
             | `.dashinclude` | bundle include |
            
             ADR: 0024 (document layers), 0011 (tab modules), 0039 (chart chrome).

---

# Dashboard shell
            
             Корневой отчёт — `@dashboard id` с end-block телом (без фигурных скобок модуля).
            
             ```text
             @dashboard demo_soak
            
             runtime
               manifest = "demo-runtime.toml"
             end runtime
            
             configuration
               sqldialect = tsql
               palette = "palettes/demo-apps.dashpalette"
             end configuration
            
             !include "layouts/soak-toolbar.dashlayout"
            
             connect
               use palette demo_apps
             end connect
            
             report
               title = "…"
               … filters / tabs …
             end report
             ```
            
             - **runtime** — только путь к TOML manifest (секреты и connectors в TOML, не в spec).
             - **configuration** — `sqldialect`, `palette`, …
             - **wiring** — `use connector`, `use palette` (имена из manifest/runtime).
             - **report** — фильтры, toolbar chrome, вкладки.
            
             Shell может ссылаться на tab modules: `tab overview as "…" dashspec "demo-overview.dashspec"`.
             LSP валидирует **только открытый файл** (без merge дочерних tab-dashspec).

---

# Фильтры (structured id-first)
            
             Канонический формат — `filter <id>` + `bind` + `show`:
            
             ```text
             filter usage_date
               bind date
                 column = usage_date
                 default = -7d..today
               end bind
               show
                 label = "Дата отчёта"
                 ref = usage_date
               end show
             end filter
             ```
            
             ## bind kinds
            
             | bind | Поля |
             |------|------|
             | `date` | `column`, `default` (range), опционально `labels { }` |
             | `field` | `column` |
             | `top` | `default` (число), опционально `min` / `max` |
            
             ## show
            
             Обязателен `label = "…"`. Для toolbar: `ref`, `widget` (`combobox`, `day`, …).
            
             Top-фильтр: label только в `show`, не `as` на строке `filter` (legacy `filter top id as "…"` тоже поддерживается).
            
             См. `FilterParser` — ADR-0010 (legacy), ADR-0037 (structured).

---

# Tab modules
            
             В shell:
            
             ```text
             tab overview as "Обзор" dashspec "demo-overview.dashspec"
             ```
            
             Tab module — отдельный `.dashspec` с `@tab id`:
            
             ```text
             @tab overview
             runtime … end runtime
             configuration … end configuration
             extensions { use card_views }
             report
               filters { … }   # опционально, локальные фильтры tab
               card …
             end report
             end tab
             ```
            
             Фильтры shell и tab module **не должны дублировать id** при full merge (Host).
             В редакторе каждый файл проверяется отдельно.
            
             ADR-0011.

---

# Карточки и диаграммы
            
             ```text
             card peak ref peak_card
               title = "…"
               data
                 datasource view demo.v_…
                 bind usage_date, app_name
               end data
               diagram demo_peak_heatmap
               chrome use heatmap_tall
             end card
             ```
            
             Preset диаграммы — в `.dashdiagram`:
            
             ```text
             @diagram demo_peak_heatmap
             heatmap
               x = …
               y = …
             end heatmap
             ```
            
             `diagram` в card ссылается на id из include / library. `chrome use <preset>` — presentation preset (ADR-0039).

---

# Страницы и layout board
            
             Несколько экранов в одном tab module:
            
             ```text
             page executive_summary
               title = "Сводка"
               toolbar period_grain, period_start, chart_top
               include layout "layouts/stakeholder-page-executive.dashlayout"
               card kpi_total_users … end card
             end page
             ```
            
             Layout (`.dashlayout`) — bracket board:
            
             ```text
             @layout stakeholder_page_executive
             scope page
            
             [ kpi_a kpi_b kpi_c ]
             [ chart_left chart_right ]
             ```
            
             Id в скобках = id карточек на странице. `scope page` привязывает layout к `page`.
            
             ADR-0020, ADR-0021, ADR-0030 (phase browse/detail).

---

# Chart chrome (`@presentation`)
            
             Пресеты в `.dashpresentation`:
            
             ```text
             @presentation heatmap_tall
             scale_value = percent
             y_max = 100
             end presentation
             ```
            
             В diagram module или card:
            
             ```text
             chrome use heatmap_tall
             ```
            
             Регистрация в shell: `!include "presentations/*.dashpresentation"`.
            
             ADR-0039.

---

# Includes
            
             ```text
             !include "layouts/soak-toolbar.dashlayout"
             !include "diagrams/*.dashdiagram"
             !include "presentations/*.dashpresentation"
             ```
            
             Пути относительно каталога `.dashspec`. Glob поддерживается.
            
             `.dashinclude` — явный bundle (layout + diagram list).
            
             ADR-0017.

---

# Extension blocks (plugins)
            
             В tab module:
            
             ```text
             extensions { use card_views }
             ```
            
             Блок `views` внутри **card** (переключатель diagram presets):
            
             ```text
             views
               default = heatmap
               heatmap
                 label = "Heatmap"
                 diagram = demo_peak_heatmap
               end heatmap
             end views
             ```
            
             Keyword `views` регистрирует plugin `card_views`. LSP/Editor знает builtin `views`;
             Host подключает plugin из runtime manifest.
            
             ADR-0032, ADR-0033.

---

# Редактор (VS Code / Cursor)
            
             1. Собрать VSIX: `scripts/package-vscode-extension.ps1`
             2. Install from VSIX (не двойной клик — через Command Palette в VS Code/Cursor)
             3. Нужен `dotnet` в PATH
            
             ## LSP
            
             - Diagnostics при open/change/save (позиции из парсера)
             - Completion: `diagram `, `chrome use `, `!include "`, keywords
             - Go to definition: diagram id, chrome preset, include path
            
             Настройки `dashspec.*` обычно не нужны. `dashspec.hostDll` — только для CLI validate.
            
             Grammar (подсветка) — best-effort; доверять LSP.

---