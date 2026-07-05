using DashSpec.Abstractions.Query;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class DashboardParseTests
{
    [Fact]
    public void ReadDashboardHeader_reads_id_and_title()
    {
        const string text = """
            @dashboard soak_id {
              runtime { manifest = "cfg.toml" }
              report "My **Title**" {
                card a as "A" {
                  diagram number { value = x }
                  datasource view dbo.t
                }
              }
            }
            """;

        Assert.Equal(("soak_id", "My **Title**"), DashSpecParser.ReadDashboardHeader(text));
    }

    [Fact]
    public void Parse_demo_sample_has_cards_and_filters()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "samples", "demo",
            "demo-soak.dashspec"));

        var text = File.ReadAllText(path);
        var doc = DashSpecParser.Parse(text, Path.GetDirectoryName(path)!);

        Assert.Equal("demo_soak", doc.Id);
        Assert.Equal("sqlserver", doc.ConnectorId);
        Assert.Equal(SqlDialect.TSql, doc.SqlDialect);
        Assert.Null(doc.DiagramLibraryPath);
        Assert.Equal("palettes/demo-apps.dashpalette", doc.PalettePath);
        Assert.Equal("demo_apps", doc.ColorPalette);
        Assert.Equal(7, doc.Cards.Count);
        Assert.Equal(8, doc.Filters.Count);
        Assert.Equal(["usage_date", "user_name", "app_name"], doc.DashboardFilters);
        Assert.True(doc.FiltersChrome.IsBarLayout);
        Assert.True(doc.FiltersChrome.IsStickyLine);
        Assert.Equal(FiltersChromeDefinition.StickyLine, doc.FiltersChrome.Sticky);
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
        var doc = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              layout grid { columns = 12 gap = 8 }
              card a as "A" {
                place { row = 1 col = 1 span = half }
                diagram number { value = x }
                datasource view dbo.t
              }
            }
            }
""");

        Assert.Equal(8, doc.Layout.GapPx);
        Assert.Equal(6, doc.Cards[0].Placement?.Span);
        Assert.Equal(1, doc.Cards[0].Placement?.Row);
    }

    [Fact]
    public void Parse_bind_block_syntax()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              filter field app_name { column = app_name as "App" }
              filters dashboard { app_name }
              card a as "A" {
                bind { app_name }
                diagram number { value = x }
                datasource view dbo.t
              }
            }
            }
""");

        Assert.Equal(["app_name"], doc.Cards[0].BoundFilters);
    }

    [Fact]
    public void Parse_card_local_filters_placement()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              filter date usage_date {
                column = usage_date as "Usage"
                default = -7d..today
              }
              filter date activity_day {
                column = bucket_start_utc as "Day"
                default = today
                widget = day
              }
              filter field app_name { column = app_name as "App" }
              filters dashboard { usage_date, app_name }
              card activity as "Activity" {
                filters { activity_day }
                bind activity_day, app_name
                diagram line { x = bucket_start_utc y = event_count }
                datasource view dbo.activity
              }
            }
            }
""");

        var card = doc.Cards.Single();
        Assert.Equal(["activity_day"], card.LocalFilters);
        Assert.True(doc.Filters.Single(f => f.Name == "activity_day").IsDayWidget);
    }

    [Fact]
    public void Parse_rejects_bound_filter_without_placement()
    {
        var ex = Assert.ThrowsAny<Exception>(() =>
            DashSpecParser.Parse("""
                @dashboard t {
                  report "T" {
                  filter date usage_date {
                    column = usage_date as "Usage"
                    default = -7d..today
                  }
                  card a as "A" {
                    bind usage_date
                    diagram number { value = x }
                    datasource view dbo.t
                  }
                }
                }
"""));

        Assert.Contains("toolbar", ex.Message);
    }

    [Fact]
    public void Parse_filters_chrome_and_tabs()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              filter date usage_date {
                column = usage_date as "Usage"
                default = -7d..today
              }
              filters chrome {
                layout = bar
                sticky = true
                apply = auto
                debounce_ms = 250
              }
              filters dashboard { usage_date }
              tab main as "Main" {
                cards { a }
              }
              card a as "A" {
                bind usage_date
                diagram number { value = x }
                datasource view dbo.t
              }
            }
            }
""");

        Assert.True(doc.FiltersChrome.IsBarLayout);
        Assert.True(doc.FiltersChrome.IsStickyLine);
        Assert.Equal(FiltersChromeDefinition.StickyLine, doc.FiltersChrome.Sticky);
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
        var line = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              filters chrome { sticky = line }
            }
            }
""");
        Assert.True(line.FiltersChrome.IsStickyLine);

        var card = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              filters chrome { sticky = card }
            }
            }
""");
        Assert.True(card.FiltersChrome.IsStickyCard);

        var none = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              filters chrome { sticky = false }
            }
            }
""");
        Assert.False(none.FiltersChrome.IsSticky);
        Assert.Equal(FiltersChromeDefinition.StickyNone, none.FiltersChrome.Sticky);
    }

    [Fact]
    public void Parse_heatmap_diagram_kind()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              card h as "H" {
                diagram heatmap {
                  x = usage_date
                  y = user_name
                  value = peak_concurrent_apps
                  height = 360
                }
                datasource view dbo.t
              }
            }
            }
""");

        Assert.Equal("heatmap", doc.Cards[0].Diagram.Kind);
        Assert.Equal(DiagramDataFamily.Matrix, DiagramKindRegistry.Resolve("heatmap").DataFamily);
    }

    [Fact]
    public void Parse_heatmap_column_as_labels()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              card h as "H" {
                diagram heatmap {
                  x = usage_date as "День"
                  y = user_name as "Пользователь"
                  value = peak_concurrent_apps as "Разных ПО"
                  tooltip = peak_apps as "Состав в пике"
                }
                datasource view dbo.t
              }
            }
            }
""");

        var diagram = doc.Cards[0].Diagram;
        Assert.Equal("usage_date", diagram.Properties["x"]);
        Assert.Equal("День", diagram.Properties["x_as"]);
        Assert.Equal("user_name", diagram.Properties["y"]);
        Assert.Equal("Пользователь", diagram.Properties["y_as"]);
        Assert.Equal("peak_concurrent_apps", diagram.Properties["value"]);
        Assert.Equal("Разных ПО", diagram.Properties["value_as"]);
        Assert.Equal("peak_apps", diagram.Properties["tooltip"]);
        Assert.Equal("Состав в пике", diagram.Properties["tooltip_as"]);
    }

    [Fact]
    public void Parse_bar_reference_column_as_label()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              card peak as "Peak" {
                diagram bar {
                  category = app_name
                  value = peak_concurrent_proxy
                  reference = purchased_seats as "Куплено"
                }
                datasource view dbo.t
              }
            }
            }
""");

        var diagram = doc.Cards[0].Diagram;
        Assert.Equal("purchased_seats", diagram.Properties["reference"]);
        Assert.Equal("Куплено", diagram.Properties["reference_as"]);
    }

    [Fact]
    public void Parse_heatmap_allows_extension_presentation_properties()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              card h as "H" {
                diagram heatmap {
                  x = usage_date
                  y = user_name
                  value = peak_concurrent_apps
                  tooltip_format = list
                  tooltip_split = ", "
                  color_scale = viridis
                }
                datasource view dbo.t
              }
            }
            }
""");

        var props = doc.Cards[0].Diagram.Properties;
        Assert.Equal("list", props["tooltip_format"]);
        Assert.Equal(", ", props["tooltip_split"]);
        Assert.Equal("viridis", props["color_scale"]);
    }

    [Fact]
    public void MatrixPresentation_defaults_tooltip_format_to_list_when_tooltip_column_set()
    {
        var card = new CardDefinition(
            "t",
            "T",
            new DiagramDefinition("heatmap", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["tooltip"] = "peak_apps",
            }),
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            BoundFilters: [],
            LocalFilters: []);

        var presentation = MatrixPresentation.FromCard(card);
        Assert.Equal(TooltipFormat.List, presentation.TooltipFormat);
    }

    [Fact]
    public void Parse_card_legend_block()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              card h as "H" {
                diagram heatmap { x = a y = b value = c }
                legend {
                  min = "мин. {min}"
                  max = "макс. {max}"
                }
                datasource view dbo.t
              }
            }
            }
""");

        var legend = doc.Cards[0].Legend;
        Assert.NotNull(legend);
        Assert.Equal("мин. {min}", legend.MinLabel);
        Assert.Equal("макс. {max}", legend.MaxLabel);
    }

    [Fact]
    public void MatrixPresentation_legend_templates_substitute_min_max()
    {
        var card = new CardDefinition(
            "t",
            "T",
            new DiagramDefinition("heatmap", new Dictionary<string, string>()),
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            BoundFilters: [],
            LocalFilters: [],
            Legend: new LegendDefinition(MinLabel: "от {min}", MaxLabel: "до {max}"));

        var presentation = MatrixPresentation.FromCard(card);
        Assert.Equal("от 1", presentation.FormatLegendMin(1, 5));
        Assert.Equal("до 5", presentation.FormatLegendMax(1, 5));
    }

    [Theory]
    [InlineData("2024-06-15", "date.short", "15.06")]
    [InlineData("2024-06-15 14:05:00", "time.short", "14:05")]
    [InlineData("2024-06-15 14:05:00", "datetime.short", "15.06 14:05")]
    [InlineData("DOMAIN\\alice.longname", "user.short", "alice.longname")]
    public void LabelFormat_formats_axis_labels(string raw, string format, string expected) =>
        Assert.Equal(expected, LabelFormat.Format(raw, format));

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
            DashSpecParser.Parse("""
                @dashboard t {
                  report "T" {
                  filter date usage_date {
                    column = usage_date as "Дата"
                    default = -7d..today
                  }
                  card a as "A" {
                    bind usage_date
                    diagram line { x = usage_date y = n }
                    datasource view dbo.t
                    where [[usage_date]]
                  }
                }
                }
"""));

        Assert.Contains("'where' is no longer used", ex.Message);
    }
    [Fact]
    public void TabLayoutCompactor_bumps_full_width_table_below_same_row_charts()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              tab s as "S" { cards { a b c } }
              card a as "A" {
                place { row = 1 col = 1 span = 6 }
                diagram bar { x = a y = b }
                datasource view dbo.a
              }
              card b as "B" {
                place { row = 1 col = 7 span = 6 }
                diagram bar { x = a y = b }
                datasource view dbo.b
              }
              card c as "C" {
                place { row = 1 col = 1 span = full }
                diagram table { columns = a, b }
                datasource view dbo.c
              }
            }
            }
""");

        var layout = TabLayoutCompactor.Compact(doc, "s");

        Assert.Equal(1, layout["a"].Row);
        Assert.Equal(1, layout["b"].Row);
        Assert.Equal(2, layout["c"].Row);
    }

}
