using System.Globalization;
using DashSpec.Abstractions.Query;
using DashSpec.Execution.Compilation;
using DashSpec.Execution.Runtime;
using DashSpec.Core.Interaction;
using DashSpec.Execution.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class DashboardParseTests
{
    static DashboardParseTests() => DashSpecTestCulture.Ensure();

    [Fact]
    public void ReadDashboardHeader_reads_id_and_title()
    {
        const string text = """
            module dashboard soak_id
                  import types from Fixtures.QueryRowTypes
              runtime
              manifest = "cfg.toml"
              end runtime
              report
              title = "My **Title**"
              card a as "A"
              diagram number
              value = x
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
            """;

        var doc = DashSpecTestRowTypes.ParseDashboard(text);
        Assert.Equal("soak_id", doc.Id);
        Assert.Equal("My **Title**", doc.Title);
    }

    [Fact]
    public void Parse_demo_sample_has_cards_and_filters()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "samples", "demo",
            "demo-soak.ds"));

        var doc = DashSpecTestRowTypes.ParseDashboard(File.ReadAllText(path), Path.GetDirectoryName(path)!);

        Assert.Equal("demo_soak", doc.Id);
        Assert.Null(doc.ConnectorId);
        Assert.Equal(SqlDialect.TSql, doc.SqlDialect);
        Assert.Null(doc.DiagramLibraryPath);
        Assert.NotNull(doc.Palette);
        Assert.Equal("demo_apps", doc.Palette!.Value.Id);
        Assert.Equal("#94a3b8", doc.Palette!.Value.Properties["default"]);
        Assert.Equal("demo_apps", doc.ColorPalette);
        Assert.Equal(18, doc.Cards.Count);
        Assert.Equal(8, doc.Filters.Count);
        Assert.Equal(["usage_date", "user_name", "app_name"], doc.DashboardFilters);
        Assert.True(doc.FiltersChrome.IsBarLayout);
        Assert.True(doc.FiltersChrome.IsStickyLine);
        Assert.Equal("line", doc.FiltersChrome.Sticky);
        Assert.True(doc.Filters.Single(f => f.Name == "app_name").IsComboboxWidget);
        Assert.True(doc.FiltersChrome.IsAutoApply);
        Assert.Equal(3, doc.Tabs.Count);
        Assert.Equal(12, doc.Layout.Columns);
        Assert.Equal("Report date", doc.Filters.Single(f => f.Name == "usage_date").Label);
        Assert.Equal("peak_apps_heatmap", doc.Cards.Single(c => c.Id == "peak_apps_heatmap").Id);
        Assert.Equal("line", doc.Cards.Single(c => c.Id == "peak_concurrent_proxy").Diagram.Kind);
        Assert.Null(doc.Cards.Single(c => c.Id == "peak_concurrent_proxy").UseCardPreset);
    }

    [Fact]
    public void Parse_layout_and_place()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              layout grid
              [] [] [] [] [] [] [] [] [] [] [] []
              end grid
              card a as "A"
              place
              row = 1
              col = 1
              span = half
              end place
              diagram number
              value = x
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        Assert.Equal(12, doc.Layout.Columns);
        Assert.Equal(6, doc.Cards[0].Placement?.Value.Span);
        Assert.Equal(1, doc.Cards[0].Placement?.Value.Row);
    }

    [Fact]
    public void Parse_bind_block_syntax()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              filter app_name
                bind field
                  column = app_name
                end bind
                show
                  label = "App"
                end show
              end filter
              filters dashboard
              app_name
              end dashboard
              card a as "A"
              bind
                app_name
              end bind
              diagram number
              value = x
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        Assert.Equal(["app_name"], doc.Cards[0].BoundFilters);
    }

    [Fact]
    public void Parse_filters_table_builds_filters_from_rows()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              filters
                | id        | bind  | column     | label | widget   | ref       | range |
                | app_name  | field | app_name   | App   | combobox | app_name  | -     |
                | usage_day | date  | usage_date | Usage | day      | usage_day | today |
              end filters
              filters dashboard
              app_name
              usage_day
              end dashboard
              card a as "A"
              bind
                app_name
              end bind
              diagram number
              value = x
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        Assert.Equal(2, doc.Filters.Count);
        var app = doc.Filters.Single(f => f.Name == "app_name");
        Assert.Equal(FilterKind.Field, app.Kind);
        Assert.Equal("app_name", app.ColumnReference?.Value);
        Assert.Equal("App", app.Label?.Value);
        Assert.Equal("combobox", app.Widget?.Value);
        Assert.Equal("app_name", app.LayoutRef?.Value);

        var day = doc.Filters.Single(f => f.Name == "usage_day");
        Assert.Equal(FilterKind.Date, day.Kind);
        Assert.Equal("usage_date", day.ColumnReference?.Value);
        Assert.Equal("day", day.Widget?.Value);
        Assert.Equal("today..today", day.DefaultExpression?.Value);
    }

    [Fact]
    public void Parse_filters_table_accepts_subset_header_and_separator_rows()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              filters
                | id        | bind  | column     | label |
                |-----------|-------|------------|-------|
                | app_name  | field | app_name   | App   |
                | usage_day | date  | usage_date | -     |
                defaults
                  filter.usage_day.range = -7d..today
                end defaults
              end filters
              filters dashboard
              app_name
              end dashboard
              card a as "A"
              bind
                app_name
              end bind
              diagram number
              value = x
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        Assert.Equal(2, doc.Filters.Count);
        var day = doc.Filters.Single(f => f.Name == "usage_day");
        Assert.Equal(FilterKind.Date, day.Kind);
        Assert.Equal("usage_date", day.ColumnReference?.Value);
        Assert.Null(day.Label);
        Assert.Null(day.Widget);
    }

    [Fact]
    public void Parse_filters_table_builds_rare_attributes()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              filters
                | id           | bind | column       | label  | widget | min | max | bind_scope | grain        | range | limit |
                | events_top   | top  | -            | Top    | -      | 0   | 500 | hidden     | -            | -     | 200  |
                | period_start | date | period_start | Period | day    | -   | -   | -          | period_grain | -1d   | -    |
              end filters
              card a as "A"
              show flow
                events_top -> [toolbar] chrome.card.a
              end show flow
              bind
                events_top
              end bind
              diagram table
                columns = x
              end table
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        var top = doc.Filters.Single(f => f.Name == "events_top");
        Assert.Equal(FilterKind.Top, top.Kind);
        Assert.Equal(0, top.MinValue?.Value);
        Assert.Equal(500, top.MaxValue?.Value);
        Assert.Equal("hidden", top.BindScopeHint?.Value);
        Assert.Null(top.ColumnReference);
        Assert.Equal("200", top.DefaultExpression?.Value);

        var period = doc.Filters.Single(f => f.Name == "period_start");
        Assert.Equal(FilterKind.Date, period.Kind);
        Assert.Equal("period_start", period.ColumnReference?.Value);
        Assert.Equal("period_grain", period.GrainFilterName?.Value);
        Assert.Equal("-1d..-1d", period.DefaultExpression?.Value);
    }

    [Fact]
    public void Parse_filters_table_coexists_with_nested_filter_blocks()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              filters
                defaults
                  filter.usage_day.range = -7d..today
                end defaults
                | id       | bind  | column   |
                | app_name | field | app_name |
                filter usage_day
                  bind date
                    column = usage_date
                  end bind
                  show
                    label = "Usage"
                  end show
                end filter
              end filters
              filters dashboard
              app_name
              usage_day
              end dashboard
              card a as "A"
              bind
                app_name
              end bind
              diagram number
              value = x
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        Assert.Equal(2, doc.Filters.Count);
        var app = doc.Filters.Single(f => f.Name == "app_name");
        Assert.Equal(FilterKind.Field, app.Kind);
        Assert.Equal("app_name", app.ColumnReference?.Value);

        var day = doc.Filters.Single(f => f.Name == "usage_day");
        Assert.Equal(FilterKind.Date, day.Kind);
        Assert.Equal("Usage", day.Label?.Value);
    }

    [Fact]
    public void Parse_filters_table_rejects_unknown_attribute()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              filters
                | id       | bind  | width |
                | app_name | field | 12    |
              end filters
              card a as "A"
              bind
                app_name
              end bind
              diagram number
              value = x
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
"""));
        Assert.Contains("width", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_filters_table_requires_bind_and_id_cells()
    {
        var missingBind = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              filters
                | id       | bind  |
                | app_name |       |
              end filters
              card a as "A"
              bind
                app_name
              end bind
              diagram number
              value = x
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
"""));
        Assert.Contains("bind", missingBind.Message, StringComparison.OrdinalIgnoreCase);

        var missingId = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              filters
                | id  | bind  |
                |     | field |
              end filters
              card a as "A"
              bind
                app_name
              end bind
              diagram number
              value = x
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
"""));
        Assert.Contains("id", missingId.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_filters_table_rejects_bad_bind_single_and_max_values()
    {
        var badBind = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              filters
                | id       | bind   | column   |
                | app_name | string | app_name |
              end filters
              card a as "A"
              bind
                app_name
              end bind
              diagram number
              value = x
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
"""));
        Assert.Contains("date, field or top", badBind.Message, StringComparison.OrdinalIgnoreCase);

        var badSingle = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              filters
                | id       | bind  | column   | single |
                | app_name | field | app_name | maybe  |
              end filters
              card a as "A"
              bind
                app_name
              end bind
              diagram number
              value = x
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
"""));
        Assert.Contains("true or false", badSingle.Message, StringComparison.OrdinalIgnoreCase);

        var badMax = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              filters
                | id         | bind | label | limit | max |
                | events_top | top  | Top   | 200   | abc |
              end filters
              card a as "A"
              show flow
                events_top -> [toolbar] chrome.card.a
              end show flow
              bind
                events_top
              end bind
              diagram table
                columns = x
              end table
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
"""));
        Assert.Contains("positive integer", badMax.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_card_local_filters_placement()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              defaults
                filter.usage_date.range = -7d..today
                filter.activity_day.range = today
              end defaults
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Usage"
                end show
              end filter
              filter activity_day
                bind date
                  column = bucket_start_utc
                end bind
                show
                  label = "Day"
                  widget = day
                end show
              end filter
              filter app_name
                bind field
                  column = app_name
                end bind
                show
                  label = "App"
                end show
              end filter
              filters dashboard
              usage_date
              app_name
              end dashboard
              card activity as "Activity"
              show flow
              activity_day -> [toolbar] chrome.card.activity
              end show flow
              bind
                activity_day, app_name
              end bind
              diagram line
              x = bucket_start_utc y
              end line
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        var card = doc.Cards.Single();
        Assert.Equal(["activity_day"], card.LocalFilters);
        Assert.True(doc.Filters.Single(f => f.Name == "activity_day").IsDayWidget);
    }

    [Fact]
    public void Parse_rejects_bound_filter_without_placement()
    {
        var ex = Assert.ThrowsAny<Exception>(() =>
            DashSpecTestRowTypes.ParseDashboard("""
                module dashboard t
                  import types from Fixtures.QueryRowTypes
                  report
                  title = "T"
                  defaults
                    filter.usage_date.range = -7d..today
                  end defaults
                  filter usage_date
                    bind date
                      column = usage_date
                    end bind
                    show
                      label = "Usage"
                    end show
                  end filter
                  card a as "A"
                  bind
                    usage_date
                  end bind
                  diagram number
                  value = x
                  end number
                  data flow { fixture_src [rows] -> [rows] __diagram__ }
                  end card
                  end report
                end dashboard
"""));

        Assert.Contains("toolbar", ex.Message);
    }

    [Fact]
    public void Parse_filters_chrome_and_tabs()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Usage"
                end show
              end filter
              filters chrome
              layout = bar
              sticky = true
              apply = auto
              debounce_ms = 250
              end chrome
              filters dashboard
              usage_date
              end dashboard
              tab main as "Main"
              cards
              a
              end cards
              end tab
              card a as "A"
              bind
                usage_date
              end bind
              diagram number
              value = x
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        Assert.True(doc.FiltersChrome.IsBarLayout);
        Assert.True(doc.FiltersChrome.IsStickyLine);
        Assert.Equal("line", doc.FiltersChrome.Sticky);
        Assert.True(doc.FiltersChrome.IsAutoApply);
        Assert.Equal(250, doc.FiltersChrome.DebounceMs);
        Assert.Equal("main", doc.Tabs[0].Id);
        Assert.Equal("Main", doc.Tabs[0].Label);
        Assert.Equal("main", doc.Cards[0].TabId);
        Assert.Equal(["a"], doc.Tabs[0].CardIds);
    }

    [Fact]
    public void Parse_filters_chrome_sticky_modes()
    {
        var line = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              filters chrome
              sticky = line
              end chrome
              end report
            end dashboard
""");
        Assert.True(line.FiltersChrome.IsStickyLine);

        var card = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              filters chrome
              sticky = card
              end chrome
              end report
            end dashboard
""");
        Assert.True(card.FiltersChrome.IsStickyCard);

        var none = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              filters chrome
              sticky = false
              end chrome
              end report
            end dashboard
""");
        Assert.False(none.FiltersChrome.IsSticky);
        Assert.Equal("none", none.FiltersChrome.Sticky);
    }

    [Fact]
    public void Parse_filters_chrome_apply_block_and_sugar()
    {
        var block = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              filters chrome
                layout = bar
                apply
                  mode = manual
                  control = button
                end apply
              end chrome
              end report
            end dashboard
""");
        Assert.False(block.FiltersChrome.IsAutoApply);
        Assert.Equal("button", block.FiltersChrome.ApplyControl);

        var sugar = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              toolbar chrome
                apply = auto
              end chrome
              end report
            end dashboard
""");
        Assert.True(sugar.FiltersChrome.IsAutoApply);
        Assert.Equal("icon", sugar.FiltersChrome.ApplyControl);
    }

    [Fact]
    public void Parse_toolbar_chrome_cells_labeled_and_inline()
    {
        var labeled = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              toolbar chrome
                layout = bar
                cells = labeled
              end chrome
              end report
            end dashboard
""");
        Assert.True(labeled.FiltersChrome.IsToolbarLabeledCells);

        var inline = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              toolbar chrome
                layout = bar
                cells = inline
              end chrome
              end report
            end dashboard
""");
        Assert.False(inline.FiltersChrome.IsToolbarLabeledCells);
    }

    [Fact]
    public void Parse_heatmap_diagram_kind()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              card h as "H"
              diagram heatmap
              x = usage_date
              y = user_name
              value = peak_concurrent_apps
              height = 360
              end heatmap
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        Assert.Equal("heatmap", doc.Cards[0].Diagram.Kind);
        Assert.Equal(DiagramDataFamily.Matrix, DiagramKindRegistry.Resolve("heatmap").DataFamily);
    }

    [Fact]
    public void Resolve_pie_and_donut_are_category_charts()
    {
        Assert.Equal(DiagramDataFamily.Chart, DiagramKindRegistry.Resolve("pie").DataFamily);
        Assert.Equal(DiagramDataFamily.Chart, DiagramKindRegistry.Resolve("donut").DataFamily);
        Assert.Equal(DiagramDataFamily.Chart, DiagramKindRegistry.Resolve("doughnut").DataFamily);
        Assert.True(DiagramKindRegistry.SupportsTopLimit("pie"));
        Assert.True(DiagramKindRegistry.SupportsTopLimit("donut"));
        Assert.True(DiagramBindings.IsRadialChart("donut"));
        Assert.True(DiagramBindings.IsCategoryChart("pie"));
    }

    [Fact]
    public void Parse_heatmap_inspect_and_tooltip_entity()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              card h as "H"
              tooltip peak_apps
              source = peak_apps
              end tooltip
              diagram heatmap
              x = usage_date as "День"
              y = user_name as "Пользователь"
              value = peak_concurrent_apps as "Разных ПО"
              end heatmap
              inspect
              use tooltip peak_apps
              label = "Состав в пике"
              as list
              end inspect
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        var card = doc.Cards[0];
        var diagram = card.Diagram;
        Assert.Equal("usage_date", diagram.Properties["x"]);
        Assert.Equal("День", diagram.Properties["x_as"]);
        Assert.Equal("user_name", diagram.Properties["y"]);
        Assert.Equal("Пользователь", diagram.Properties["y_as"]);
        Assert.Equal("peak_concurrent_apps", diagram.Properties["value"]);
        Assert.Equal("Разных ПО", diagram.Properties["value_as"]);
        Assert.NotNull(card.Tooltip);
        Assert.Equal("peak_apps", card.Tooltip!.Value.Id);
        Assert.NotNull(card.Inspect);
        Assert.Equal("peak_apps", card.Inspect!.Value.TooltipId?.Value);
        Assert.Equal("Состав в пике", card.Inspect.Value.Label?.Value);
        Assert.Equal("list", card.Inspect!.Value.Format);
    }

    [Fact]
    public void Parse_heatmap_allows_extension_presentation_properties()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              card h as "H"
              diagram heatmap
              x = usage_date
              y = user_name
              value = peak_concurrent_apps
              color_scale = viridis
              end heatmap
              inspect
              use tooltip peak_apps
              as list
              split = ", "
              end inspect
              tooltip peak_apps
              source = peak_apps
              end tooltip
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        var props = doc.Cards[0].Diagram.Properties;
        Assert.Equal("viridis", props["color_scale"]);
        Assert.Equal("list", doc.Cards[0].Inspect!.Value.Format);
        Assert.Equal(", ", doc.Cards[0].Inspect!.Value.Split);
    }

    [Fact]
    public void MatrixPresentation_defaults_tooltip_format_to_list_when_inspect_has_split()
    {
        var card = CardTestData.Create(
            "t",
            "T",
            new DiagramDefinition("heatmap", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), null),
            DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, "dbo.t"),
            BoundFilters: [],
            LocalFilters: [],
            Inspect: new InspectPresentation
            {
                TooltipId = Microsoft.FSharp.Core.FSharpOption<string>.Some("peak_apps"),
                Label = Microsoft.FSharp.Core.FSharpOption<string>.None,
                Format = "list",
                Split = "; ",
            },
            Tooltip: new TooltipDefinition("peak_apps", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["value"] = "peak_apps",
            }, "{value}"));

        var presentation = MatrixPresentation.FromCard(card);
        Assert.Equal(TooltipFormat.List, presentation.TooltipFormat);
        Assert.True(presentation.HasTooltip);
    }

    [Fact]
    public void Parse_bar_reference_column_as_label()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              card peak as "Peak"
              diagram bar
              category = app_name
              value = peak_concurrent_proxy
              reference = purchased_seats as "Куплено"
              end bar
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        var diagram = doc.Cards[0].Diagram;
        Assert.Equal("purchased_seats", diagram.Properties["reference"]);
        Assert.Equal("Куплено", diagram.Properties["reference_as"]);
    }

    [Fact]
    public void Parse_card_legend_block()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
                title = "T"
              card h as "H"
                diagram heatmap
                  x = a y
                  value = c
                end heatmap
                legend
                  min = "мин. {min}"
                  max = "макс. {max}"
                
                end legend
                data flow { fixture_src [rows] -> [rows] __diagram__ }
              
              end card
            
              end report
            
            end dashboard
""");

        var legend = doc.Cards[0].Legend;
        Assert.NotNull(legend);
        Assert.Equal("мин. {min}", legend.Value.MinLabel?.Value);
        Assert.Equal("макс. {max}", legend.Value.MaxLabel?.Value);
    }

    [Fact]
    public void MatrixPresentation_legend_templates_substitute_min_max()
    {
        var card = CardTestData.Create(
            "t",
            "T",
            new DiagramDefinition("heatmap", new Dictionary<string, string>(), null),
            DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, "dbo.t"),
            BoundFilters: [],
            LocalFilters: [],
            Legend: new LegendDefinition
            {
                MinLabel = Microsoft.FSharp.Core.FSharpOption<string>.Some("от {min}"),
                MaxLabel = Microsoft.FSharp.Core.FSharpOption<string>.Some("до {max}"),
            });

        var presentation = MatrixPresentation.FromCard(card);
        Assert.Equal("от 1", presentation.FormatLegendMin(1, 5));
        Assert.Equal("до 5", presentation.FormatLegendMax(1, 5));
    }

    [Fact]
    public void MatrixPresentation_reads_visible_rows_from_chrome_preset_and_series_fallback()
    {
        var cardWithChrome = CardTestData.Create(
            "t",
            "T",
            new DiagramDefinition("heatmap", new Dictionary<string, string>(), null),
            DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, "dbo.t"),
            BoundFilters: [],
            LocalFilters: [],
            Presentation: new PresentationBlock(
                null,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["visible_rows"] = "6",
                }));

        Assert.Equal(6, MatrixPresentation.FromCard(cardWithChrome).VisibleRows);

        var cardWithSeries = CardTestData.Create(
            "t",
            "T",
            new DiagramDefinition("heatmap", new Dictionary<string, string>(), null),
            DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, "dbo.t"),
            BoundFilters: [],
            LocalFilters: [],
            SeriesTransform: new SeriesTransformBlock(null, 8, "Other"));

        Assert.Equal(8, MatrixPresentation.FromCard(cardWithSeries).VisibleRows);
    }

    [Theory]
    [InlineData("2024-06-15", "date.short", "15.06")]
    [InlineData("2024-06-15 14:05:00", "time.short", "14:05")]
    [InlineData("2024-06-15 14:05:00", "datetime.short", "15.06 14:05")]
    [InlineData("DOMAIN\\alice.longname", "user.short", "alice.longname")]
    public void LabelFormat_formats_axis_labels(string raw, string format, string expected) =>
        Assert.Equal(expected, LabelFormat.Format(raw, format));

    [Fact]
    public void LabelFormat_FormatObject_converts_utc_to_display_time_zone()
    {
        LabelFormat.DisplayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Russian Standard Time" : "Europe/Moscow");
        try
        {
            var utc = new DateTime(2024, 6, 15, 11, 5, 0, DateTimeKind.Utc);
            Assert.Equal("15.06 14:05", LabelFormat.FormatObject(utc, "datetime.short"));

            var sqlUnspecified = new DateTime(2024, 6, 15, 11, 5, 0, DateTimeKind.Unspecified);
            Assert.Equal("15.06 14:05", LabelFormat.FormatObject(sqlUnspecified, "datetime.short"));
        }
        finally
        {
            LabelFormat.DisplayTimeZone = null;
        }
    }

    [Fact]
    public void HeatmapCellFilterResolver_takes_civil_time_label_as_is()
    {
        var moscow = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Russian Standard Time" : "Europe/Moscow");
        LabelFormat.DisplayTimeZone = moscow;
        try
        {
            var filter = FilterTestData.Create(
                FilterKind.Field,
                "activity_bucket_time",
                null,
                "bucket_start_utc");
            var index = new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                [filter.Name] = filter,
            };
            var filters = new FilterState();
            filters.SetDate("activity_slot", new DateOnly(2024, 6, 15), new DateOnly(2024, 6, 15));
            var context = new HeatmapCellContext(0, 0, "14:05", "host", 3, null);

            var overlay = HeatmapCellFilterResolver.BuildOverlay(
                [CardClickEffect.NewSetFilterFromField(filter.Name, "x")],
                context,
                index,
                filters,
                ["activity_slot"]);

            Assert.True(overlay.TryGetField(filter.Name, out var values));
            Assert.Contains("2024-06-15T14:05:00", values);
        }
        finally
        {
            LabelFormat.DisplayTimeZone = null;
        }
    }

    [Fact]
    public void DrillSlotTitleResolver_fills_field_and_date_placeholders()
    {
        var overlay = new CardCellDrillOverlay();
        overlay.SetField("app_name", "AVEVA E3D");
        overlay.SetDate("usage_date", new DateOnly(2026, 10, 16), new DateOnly(2026, 10, 16));

        var title = DrillSlotTitleResolver.Resolve("Запуски: {app_name} · {usage_date}", overlay);

        Assert.Equal("Запуски: AVEVA E3D · 16.10", title);
    }

    [Fact]
    public void DrillSlotTitleResolver_null_when_placeholder_unfillable_and_static_title_passes()
    {
        Assert.Null(DrillSlotTitleResolver.Resolve("Запуски: {app_name}", null));
        Assert.Null(DrillSlotTitleResolver.Resolve("Запуски: {app_name}", new CardCellDrillOverlay()));
        Assert.Null(DrillSlotTitleResolver.Resolve(null, null));
        Assert.Null(DrillSlotTitleResolver.Resolve("   ", null));

        var overlay = new CardCellDrillOverlay();
        overlay.SetField("app_name", "AutoCAD");
        Assert.Equal("Запуски за период", DrillSlotTitleResolver.Resolve("Запуски за период", overlay));
        Assert.Equal("Запуски: AutoCAD", DrillSlotTitleResolver.Resolve("Запуски: {app_name}", overlay));
    }

    [Fact]
    public void LabelFormat_month_short_formats_month_name()
    {
        LabelFormat.DisplayTimeZone = null;
        Assert.Equal(
            "окт.",
            LabelFormat.FormatObject(new DateTime(2026, 10, 1), "month.short"));
    }

    [Theory]
    [InlineData("sticky", MatrixAxisScrollMode.Sticky)]
    [InlineData("none", MatrixAxisScrollMode.None)]
    [InlineData("scroll", MatrixAxisScrollMode.None)]
    public void VizAxisPresentationParser_ParseAxisScroll(string raw, MatrixAxisScrollMode expected)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["axis_scroll"] = raw,
        };
        Assert.Equal(expected, VizAxisPresentationParser.ParseAxisScroll(props));
    }

    [Fact]
    public void LabelFormat_FormatObject_parses_iso_dates_without_day_month_swap()
    {
        LabelFormat.DisplayTimeZone = null;
        var value = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal("15.06.2024", LabelFormat.FormatObject(value, "date.full"));
    }

    [Fact]
    public void LabelFormat_Format_skips_reformat_for_preformatted_time_labels()
    {
        LabelFormat.DisplayTimeZone = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Russian Standard Time" : "Europe/Moscow");
        try
        {
            Assert.Equal("14:05", LabelFormat.Format("14:05", "time.short"));
        }
        finally
        {
            LabelFormat.DisplayTimeZone = null;
        }
    }

    [Fact]
    public void GrainFilterPresentation_chip_uses_report_date_format_not_iso()
    {
        LabelFormat.SetReportDefaults(TestReportFormatDefaults.Create(dateFormat: "dd.MM"));
        try
        {
            var chip = GrainFilterPresentation.FormatChipValue(
                new DateOnly(2026, 8, 3),
                new DateOnly(2026, 8, 3),
                "day");
            Assert.Equal("03.08", chip);
        }
        finally
        {
            LabelFormat.ClearReportDefaults();
        }
    }

    [Fact]
    public void LabelFormat_FormatObject_supports_csharp_custom_pattern()
    {
        LabelFormat.DisplayTimeZone = null;
        var value = new DateTime(2024, 6, 15, 14, 5, 0, DateTimeKind.Utc);
        Assert.Equal("15.06.2024 14:05", LabelFormat.FormatObject(value, "dd.MM.yyyy HH:mm"));
    }

    [Fact]
    public void LabelFormat_FormatObject_system_uses_ui_culture()
    {
        LabelFormat.DisplayTimeZone = null;
        LabelFormat.UiCulture = CultureInfo.GetCultureInfo("en-US");
        try
        {
            var value = new DateTime(2024, 6, 15, 14, 5, 0, DateTimeKind.Unspecified);
            Assert.Contains("6/15/2024", LabelFormat.FormatObject(value, "system"));
        }
        finally
        {
            DashSpecTestCulture.Ensure();
        }
    }

    [Theory]
    [InlineData("default", "end default")]
    [InlineData("defaults", "end defaults")]
    public void Parse_report_format_defaults_block_sets_format_defaults(string blockKeyword, string endKeyword)
    {
        var document = DashSpecTestRowTypes.ParseDashboard($$"""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
                title = "T"
                {{blockKeyword}}
                  culture = "en-GB"
                  time_format = "HH:mm"
                  date_format = "dd.MM.yyyy"
                  datetime_format = "dd.MM.yyyy HH:mm"
                {{endKeyword}}
              end report
            end dashboard
            """);

        Assert.Equal("en-GB", document.ResolvedFormatDefaults.Culture?.Value);
        Assert.Equal("HH:mm", document.ResolvedFormatDefaults.TimeFormat?.Value);
        Assert.Equal("dd.MM.yyyy", document.ResolvedFormatDefaults.DateFormat?.Value);
        Assert.Equal("dd.MM.yyyy HH:mm", document.ResolvedFormatDefaults.DateTimeFormat?.Value);
    }

    [Fact]
    public void LabelFormat_report_defaults_apply_when_diagram_format_omitted()
    {
        LabelFormat.DisplayTimeZone = null;
        LabelFormat.SetReportDefaults(TestReportFormatDefaults.Create(
            timeFormat: "HH:mm",
            dateFormat: "dd.MM.yyyy",
            dateTimeFormat: "dd.MM.yyyy HH:mm"));
        try
        {
            var value = new DateTime(2024, 6, 15, 14, 5, 0, DateTimeKind.Unspecified);
            Assert.Equal("15.06.2024 14:05", LabelFormat.FormatObject(value));
            Assert.Equal("15.06.2024", LabelFormat.FormatObject(new DateOnly(2024, 6, 15)));
            Assert.Equal("14:05", PayloadRowFormatters.FormatChartAxisLabel(value, null));
            var midnight = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Unspecified);
            Assert.Equal("15.06.2024", PayloadRowFormatters.FormatChartAxisLabel(midnight, null));
            Assert.Equal("15.06.2024", PayloadRowFormatters.FormatHeatmapLabel(new DateOnly(2024, 6, 15)));
        }
        finally
        {
            LabelFormat.ClearReportDefaults();
        }
    }

    [Fact]
    public void LabelFormat_diagram_format_overrides_report_default()
    {
        LabelFormat.SetReportDefaults(TestReportFormatDefaults.Create(timeFormat: "HH:mm"));
        try
        {
            var value = new DateTime(2024, 6, 15, 14, 5, 0, DateTimeKind.Unspecified);
            Assert.Equal("15.06 14:05", LabelFormat.FormatObject(value, LabelFormat.ResolveTimeFormat("datetime.short")));
        }
        finally
        {
            LabelFormat.ClearReportDefaults();
        }
    }

    [Theory]
    [InlineData("**bold**", "<strong>bold</strong>")]
    [InlineData("a & b", "a &amp; b")]
    [InlineData("макс. **5**", "макс. <strong>5</strong>")]
    [InlineData("<color:blue>x</color>", """<span style="color:#3b82f6">x</span>""")]
    [InlineData("""//italics//""", "<em>italics</em>")]
    public void CreoleSubset_to_html(string input, string expected) =>
        Assert.Equal(expected, CreoleSubset.ToHtml(input));

    [Fact]
    public void Parse_rejects_legacy_where_block()
    {
        var ex = Assert.ThrowsAny<Exception>(() =>
            DashSpecTestRowTypes.ParseDashboard("""
                module dashboard t
                  import types from Fixtures.QueryRowTypes
                  report
                  title = "T"
                  defaults
                    filter.usage_date.range = -7d..today
                  end defaults
                  filter usage_date
                    bind date
                      column = usage_date
                    end bind
                    show
                      label = "Дата"
                    end show
                  end filter
                  card a as "A"
                  bind
                    usage_date
                  end bind
                  diagram line
                  x = usage_date y
                  end line
                  data flow { fixture_src [rows] -> [rows] __diagram__ }
                  where [[usage_date]]
                  end card
                  end report
                end dashboard
"""));

        Assert.Contains("'where' is no longer used", ex.Message);
    }
    [Fact]
    public void TabLayoutCompactor_bumps_full_width_table_below_same_row_charts()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
              report
              title = "T"
              tab s as "S"
              cards
              a
              b
              c
              end cards
              end tab
              card a as "A"
              place
              row = 1
              col = 1
              span = 6
              end place
              diagram bar
              x = a y
              end bar
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              card b as "B"
              place
              row = 1
              col = 1
              span = 6
              end place
              diagram bar
              x = a y
              end bar
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              card c as "C"
              place
              row = 1
              col = 1
              span = full
              end place
              diagram table
              columns = a, b
              end table
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        var layout = TabLayoutCompactor.Compact(doc, "s");

        Assert.Equal(1, layout["a"].Row);
        Assert.Equal(2, layout["b"].Row);
        Assert.Equal(3, layout["c"].Row);
    }

}
