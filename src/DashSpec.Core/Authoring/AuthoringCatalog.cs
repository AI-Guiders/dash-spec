namespace DashSpec.Core.Authoring;

/// <summary>Machine-readable authoring topics. Human text lives in XML docs; see <c>DashSpec.DocGen</c>.</summary>
public static class AuthoringCatalog
{
    /// <summary>
    /// # Обзор DashSpec
    ///
    /// DashSpec — DSL для дашбордов. Единица компиляции — один файл `.ds` с заголовком
    /// `module &lt;kind&gt; &lt;id&gt;` и опциональной строкой `namespace &lt;Namespace&gt;`.
    ///
    /// Источник правды — парсер `DashSpec.Core` (Host и LSP используют один и тот же код).
    ///
    /// ## Виды юнитов
    ///
    /// | Заголовок | Назначение |
    /// |-----------|------------|
    /// | `module dashboard &lt;id&gt;` | dashboard shell |
    /// | `module tab &lt;id&gt;` | tab module |
    /// | `module diagram &lt;id&gt;` | диаграмма (kind block: `heatmap`, `bar`, …) |
    /// | `module presentation &lt;id&gt;` | chrome preset |
    /// | `module layout &lt;id&gt;` | toolbar / card grid |
    /// | `module palette &lt;id&gt;` | цветовая палитра |
    /// | `module types &lt;id&gt;` | row types |
    /// | `module flow &lt;id&gt;` | поток данных |
    /// | `module tooltip &lt;id&gt;` | tooltip |
    /// | `module catalog &lt;id&gt;` | каталог entry → dashspec |
    /// | `module host &lt;id&gt;` | host shell (layouts) |
    ///
    /// Юниты ссылаются друг на друга импортом по namespace: `import &lt;kind&gt; from &lt;Namespace&gt; [as &lt;alias&gt;]`.
    ///
    /// ADR: 0024 (document layers), 0011 (tab modules), 0039 (chart chrome), 0098 (import by namespace),
    /// 0101 (единый тип юнита), 0102 (один язык).
    /// </summary>
    [AuthoringTopic("overview", 10)]
    public static class Overview { }

    /// <summary>
    /// # Dashboard shell
    ///
    /// Корневой отчёт — `module dashboard &lt;id&gt;` с end-block телом.
    ///
    /// ```text
    /// module dashboard demo_soak
    ///   namespace Demo.Soak
    ///
    ///   import types from Demo.Types
    ///   import layouts from Demo.Layouts
    ///   import palettes from Demo.Palettes
    ///   import flows from Demo.Flows as report_flow
    ///   import presentations from DashSpec.Std
    ///   import tabs from Demo.Analytics
    ///
    /// runtime
    ///   manifest = "demo.toml"
    /// end runtime
    ///
    /// configuration
    ///   sqldialect = tsql
    /// end configuration
    ///
    /// connect
    ///   use palette demo_apps
    ///   use flow report_flow
    /// end connect
    ///
    /// report
    ///   title = "…"
    ///   … filters / tabs …
    /// end report
    /// ```
    ///
    /// - **runtime** — только путь к TOML manifest (секреты и providers в TOML, не в spec).
    /// - **configuration** — `sqldialect`, …
    /// - **connect** — `use palette &lt;id&gt;` (id из `import palettes`), `use flow &lt;alias&gt;` (alias из `import flows`); `use provider` (id или infer) на `source` в flow-юните; card `datasource { infer … }` — то же намерение.
    /// - **report** — фильтры, toolbar chrome, вкладки.
    ///
    /// Shell ссылается на tab modules по id: `import tabs from Demo.Analytics` + `tab analytics as "…"` без тела.
    /// LSP валидирует **только открытый файл** (без merge дочерних tab modules).
    /// </summary>
    [AuthoringTopic("dashboard-shell", 20)]
    public static class DashboardShell { }

    /// <summary>
    /// # Фильтры (structured id-first)
    ///
    /// Канонический формат — `filter &lt;id&gt;` + `bind` + `show`:
    ///
    /// ```text
    /// filter usage_date
    ///   bind date
    ///     column = usage_date
    ///     default = -7d..today
    ///   end bind
    ///   show
    ///     label = "Дата отчёта"
    ///     ref = usage_date
    ///   end show
    /// end filter
    /// ```
    ///
    /// ## bind kinds
    ///
    /// | bind | Поля |
    /// |------|------|
    /// | `date` | `column`, `default` (range), опционально `labels { }` |
    /// | `field` | `column` |
    /// | `top` | `default` (число), опционально `min` / `max` |
    ///
    /// ## show
    ///
    /// Обязателен `label = "…"`. Для toolbar: `ref`, `widget` (`combobox`, `day`, …).
    ///
    /// Top-фильтр: label только в `show`, не `as` на строке `filter` (legacy `filter top id as "…"` тоже поддерживается).
    ///
    /// См. `FilterParser` — ADR-0010 (legacy), ADR-0037 (structured).
    /// </summary>
    [AuthoringTopic("filters", 30)]
    public static class Filters { }

    /// <summary>
    /// # Tab modules
    ///
    /// В shell — ссылка по id, без тела:
    ///
    /// ```text
    /// module dashboard demo_soak
    ///   import tabs from Demo.Analytics
    ///
    /// report
    ///   tab analytics as "Обзор"
    /// end report
    /// ```
    ///
    /// Tab module — юнит `module tab &lt;id&gt;`:
    ///
    /// ```text
    /// module tab analytics
    ///   namespace Demo.Analytics
    ///   import palettes from Demo.Palettes
    ///
    /// runtime
    ///   manifest = "demo.toml"
    /// end runtime
    ///
    /// report
    ///   filters { … }   # опционально, локальные фильтры tab
    ///   card …
    /// end report
    /// end tab
    /// ```
    ///
    /// Вкладка с телом (`cards` или `layout` на следующих строках) объявляется inline;
    /// вкладка без тела берёт юнит по id из `import tabs`.
    ///
    /// Фильтры shell и tab module **не должны дублировать id** при full merge (Host).
    /// В редакторе каждый файл проверяется отдельно.
    ///
    /// ADR-0011, ADR-0098.
    /// </summary>
    [AuthoringTopic("tabs", 40)]
    public static class Tabs { }

    /// <summary>
    /// # Карточки и диаграммы
    ///
    /// ```text
    /// card peak ref peak_card
    ///   title = "…"
    ///   data
    ///     datasource infer view demo.v_…
    ///     bind usage_date, app_name
    ///   end data
    ///   diagram demo_peak_heatmap
    ///   chrome use heatmap_tall
    /// end card
    /// ```
    ///
    /// Preset диаграммы — юнит `module diagram &lt;id&gt;`:
    ///
    /// ```text
    /// module diagram demo_peak_heatmap
    ///   namespace Demo.Diagrams
    ///
    /// heatmap
    ///   x = …
    ///   y = …
    /// end heatmap
    /// ```
    ///
    /// `diagram` в card ссылается на id юнита, подключённого через `import diagrams`.
    /// `chrome use &lt;preset&gt;` — presentation preset (ADR-0039), id из `import presentations`.
    /// </summary>
    [AuthoringTopic("cards-diagrams", 50)]
    public static class CardsAndDiagrams { }

    /// <summary>
    /// # Chart chrome (`presentation`)
    ///
    /// Пресеты — юнит `module presentation &lt;id&gt;`:
    ///
    /// ```text
    /// module presentation heatmap_tall
    ///   namespace Demo.Chrome
    ///
    /// scale_value = percent
    /// y_max = 100
    /// end presentation
    /// ```
    ///
    /// Подключение в shell или tab module:
    ///
    /// ```text
    /// import presentations from Demo.Chrome
    /// ```
    ///
    /// В diagram module или card:
    ///
    /// ```text
    /// chrome use heatmap_tall
    /// ```
    ///
    /// Стандартные пресеты не подгружаются неявно: `import presentations from DashSpec.Std`.
    ///
    /// ADR-0039, ADR-0098.
    /// </summary>
    [AuthoringTopic("chrome", 60)]
    public static class Chrome { }

    /// <summary>
    /// # Imports
    ///
    /// Юниты ссылаются друг на друга импортом по namespace:
    ///
    /// ```text
    /// module dashboard demo_soak
    ///   namespace Demo.Soak
    ///   import layouts from Demo.Layouts
    ///   import palettes from Demo.Palettes
    ///   import flows from Demo.Flows as report_flow
    ///   import presentations from DashSpec.Std
    /// ```
    ///
    /// Kinds: `diagrams`, `layouts`, `presentations`, `tooltips`, `types`, `flows`, `palettes`, `tabs`.
    ///
    /// Из namespace берутся только юниты запрошенного вида. Namespace объявляется строкой
    /// `namespace &lt;NS&gt;` в заголовке юнита; стандартные презенты живут в `DashSpec.Std`.
    ///
    /// `as &lt;alias&gt;` задаёт имя для ссылок по alias (`use flow report_flow`).
    ///
    /// ADR-0098, ADR-0101, ADR-0102.
    /// </summary>
    [AuthoringTopic("imports", 70)]
    public static class Imports { }

    /// <summary>
    /// # Extension blocks (plugins)
    ///
    /// В tab module:
    ///
    /// ```text
    /// extensions { use card_views }
    /// ```
    ///
    /// Блок `views` внутри **card** (переключатель diagram presets):
    ///
    /// ```text
    /// views
    ///   default = heatmap
    ///   heatmap
    ///     label = "Heatmap"
    ///     diagram = demo_peak_heatmap
    ///   end heatmap
    /// end views
    /// ```
    ///
    /// Keyword `views` регистрирует plugin `card_views`. LSP/Editor знает builtin `views`;
    /// Host подключает plugin из runtime manifest.
    ///
    /// ADR-0032, ADR-0033.
    /// </summary>
    [AuthoringTopic("extensions", 80)]
    public static class Extensions { }

    /// <summary>
    /// # Редактор (VS Code / Cursor)
    ///
    /// 1. Собрать VSIX: `scripts/package-vscode-extension.ps1`
    /// 2. Install from VSIX (не двойной клик — через Command Palette в VS Code/Cursor)
    /// 3. Нужен `dotnet` в PATH
    ///
    /// ## LSP
    ///
    /// - Diagnostics при open/change/save (позиции из парсера)
    /// - Completion: `diagram `, `chrome use `, `import &lt;kind&gt; from `, keywords
    /// - Go to definition: diagram id, chrome preset, namespace import
    ///
    /// Настройки `dashspec.*` обычно не нужны. `dashspec.hostDll` — только для CLI validate.
    ///
    /// Grammar (подсветка) — best-effort; доверять LSP.
    /// </summary>
    [AuthoringTopic("editor", 90)]
    public static class Editor { }

    /// <summary>
    /// # Страницы и layout board
    ///
    /// Несколько экранов в одном tab module:
    ///
    /// ```text
    /// page executive_summary
    ///   title = "Сводка"
    ///   toolbar period_grain, period_start, chart_top
    ///   include layout "layouts/stakeholder-page-executive.dashlayout"
    ///   card kpi_total_users … end card
    /// end page
    /// ```
    ///
    /// Layout — юнит `module layout &lt;id&gt;` с board:
    ///
    /// ```text
    /// module layout stakeholder_page_executive
    ///   namespace Demo.Layouts
    ///
    /// scope page
    ///
    /// [ kpi_a kpi_b kpi_c ]
    /// [ chart_left chart_right ]
    /// ```
    ///
    /// Id в скобках = id карточек на странице. `scope page` привязывает layout к `page`.
    ///
    /// Привязка board к странице/toolbar остаётся путевой (`include layout "…"`, `include toolbar "…"`),
    /// а не импортом: это ссылка на board внутри документа, не юнит-ссылка (ADR-0021, ADR-0022, ADR-0101).
    ///
    /// ADR-0020, ADR-0021, ADR-0030 (phase browse/detail).
    /// </summary>
    [AuthoringTopic("pages-layout", 55)]
    public static class PagesAndLayout { }
}
