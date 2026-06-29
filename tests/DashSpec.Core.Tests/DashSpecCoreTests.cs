using DashSpec.Abstractions.Query;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class DashSpecParserTests
{
    [Fact]
    public void Tokenize_activity_slot_is_single_ident()
    {
        var tokens = DashSpecParser.Tokenize("filter date activity_slot {");
        var idents = tokens.Where(t => t.Kind == TokenKind.Ident).Select(t => t.Value).ToList();
        Assert.Equal(["filter", "date", "activity_slot"], idents);
    }

    [Fact]
    public void Parse_filter_date_block_with_underscore_name()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date activity_slot {
                column = bucket_start_utc as "Day"
                default = today
                widget = day
              }
            }
            """);

        Assert.Equal("activity_slot", doc.Filters.Single().Name);
        Assert.True(doc.Filters.Single().IsDayWidget);
    }

    [Fact]
    public void Parse_on_syntax_filter_followed_by_block_filter()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date on usage_date as "Дата отчёта" default -7d..today
              filter date activity_slot {
                column = bucket_start_utc as "День"
                default = today
                widget = day
              }
            }
            """);

        Assert.Equal(2, doc.Filters.Count);
        Assert.Equal("activity_slot", doc.Filters.Last().Name);
    }

    [Fact]
    public void Parse_top_filter_inline_default()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter top events_top as "Строк (TOP)" default 200
            }
            """);

        Assert.Equal("events_top", doc.Filters.Single().Name);
        Assert.Equal("200", doc.Filters.Single().DefaultExpression);
    }

    [Fact]
    public void Parse_period_grain_then_top_filter()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter field period_grain on demo.v_peak_concurrent_by_period.period_grain as "Масштаб: день / месяц / год"
              filter top events_top as "Строк (TOP)" default 200
            }
            """);

        Assert.Equal(2, doc.Filters.Count);
    }

    [Fact]
    public void Parse_soak_filters_up_to_period_grain()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date on usage_date as "Дата отчёта" default -7d..today
              filter date activity_slot {
                column = bucket_start_utc as "День"
                default = today
                widget = day
              }
              filter date period_start on period_start as "Начало периода" default -7d..today
              filter field app_name on demo.v_daily_active_users.app_name as "Продукты" widget combobox
              filter field user_name on demo.v_events_detail.user_sam as "Пользователь" widget combobox
              filter field period_grain on demo.v_peak_concurrent_by_period.period_grain as "Масштаб: день / месяц / год"
            }
            """);

        Assert.Equal(6, doc.Filters.Count);
    }

    [Fact]
    public void Parse_soak_filters_section()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date on usage_date as "Дата отчёта" default -7d..today
              filter date activity_slot {
                column = bucket_start_utc as "День"
                default = today
                widget = day
              }
              filter date period_start on period_start as "Начало периода" default -7d..today
              filter field app_name on demo.v_daily_active_users.app_name as "Продукты" widget combobox
              filter field user_name on demo.v_events_detail.user_sam as "Пользователь" widget combobox
              filter field period_grain on demo.v_peak_concurrent_by_period.period_grain as "Масштаб: день / месяц / год"
              filter top events_top as "Строк (TOP)" default 200
              filter top idle_top as "Строк (TOP)" default 100
            }
            """);

        Assert.Equal(8, doc.Filters.Count);
    }

    [Fact]
    public void ReadConfigPath_returns_relative_toml_path()
    {
        const string text = """
            @config "demo.toml"

            @dashboard t
            dashboard "T" {
              card a as "A" {
                diagram number { value = x }
                datasource view dbo.t
              }
            }
            """;

        Assert.Equal("demo.toml", DashSpecParser.ReadConfigPath(text));
        Assert.Equal(("t", "T"), DashSpecParser.ReadDashboardHeader(text));
        Assert.Equal("t", DashSpecParser.Parse(text).Id);
    }

    [Fact]
    public void ReadSqlDialect_parses_file_directive()
    {
        const string text = """
            @config "cfg.toml"
            @sqldialect postgres

            @dashboard t
            dashboard "T" {
              card a as "A" {
                diagram number { value = x }
                datasource view dbo.t
              }
            }
            """;

        Assert.Equal(SqlDialect.Postgres, DashSpecParser.ReadSqlDialect(text));
        Assert.Equal(SqlDialect.Postgres, DashSpecParser.Parse(text).SqlDialect);
    }

    [Fact]
    public void Parse_sql_datasource_reads_inline_select()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date {
                column = usage_date as "Дата"
                default = -7d..today
              }
              filters dashboard { usage_date }
              card a as "A" {
                bind usage_date
                diagram bar { x = user_sam y = peak }
                datasource sql "SELECT user_sam, peak FROM demo.v_x GROUP BY user_sam"
              }
            }
            """);

        var card = doc.Cards[0];
        Assert.Equal(DataSourceKind.Sql, card.DataSource.Kind);
        Assert.Contains("GROUP BY", card.DataSource.Value);
    }

    [Theory]
    [InlineData("DELETE FROM t")]
    [InlineData("SELECT 1; DROP TABLE t")]
    [InlineData("INSERT INTO t SELECT 1")]
    [InlineData("SELECT * INTO hack FROM t")]
    [InlineData("SELECT 1 -- evil")]
    public void Parse_sql_datasource_rejects_non_readonly(string sqlBody)
    {
        var spec = $$"""
            @dashboard t
            dashboard "T" {
              filter date usage_date {
                column = usage_date as "Дата"
                default = -7d..today
              }
              filters dashboard { usage_date }
              card a as "A" {
                bind usage_date
                diagram bar { x = a y = b }
                datasource sql "{{sqlBody.Replace("\"", "\\\"")}}"
              }
            }
            """;

        var ex = Assert.ThrowsAny<Exception>(() => DashSpecParser.Parse(spec));
        Assert.Contains("datasource sql", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_sql_datasource_allows_keyword_inside_string_literal()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date {
                column = usage_date as "Дата"
                default = -7d..today
              }
              filters dashboard { usage_date }
              card a as "A" {
                bind usage_date
                diagram bar { x = title y = n }
                datasource sql "SELECT title FROM t WHERE title = 'DELETE is ok'"
              }
            }
            """);

        Assert.Equal(DataSourceKind.Sql, doc.Cards[0].DataSource.Kind);
    }

    [Fact]
    public void Parse_presentation_and_transform_blocks()
    {
        var doc = DashSpecParser.Parse("""
            @diagramlibrary "lib.toml"
            @dashboard t
            dashboard "T" {
              card c as "C" {
                diagram line {
                  x = usage_date
                  y = value
                  series = app_name
                }
                presentation { use = line_bottom legend = right }
                transform series { max = 4 other = "Прочее" }
                datasource view dbo.t
              }
            }
            """);

        var card = doc.Cards[0];
        Assert.Equal("lib.toml", doc.DiagramLibraryPath);
        Assert.NotNull(card.Presentation);
        Assert.Equal("line_bottom", card.Presentation!.UsePreset);
        Assert.Equal("right", card.Presentation.Properties["legend"]);
        Assert.NotNull(card.SeriesTransform);
        Assert.Equal(4, card.SeriesTransform!.Max);
        Assert.Equal("Прочее", card.SeriesTransform.OtherLabel);
    }

    [Fact]
    public void CardChromeResolver_merges_library_presets()
    {
        var libraryPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "samples", "demo",
            "demo-diagram-library.toml"));

        var library = SpecLibrary.LoadFile(libraryPath);
        var card = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              card c as "C" {
                diagram line { x = a y = b series = s }
                presentation { use = line_bottom_300 }
                transform series { use = top5 }
                datasource view dbo.t
              }
            }
            """).Cards[0];

        var presentation = CardChromeResolver.ResolveChartPresentation(card, library);
        Assert.Equal("bottom", presentation.Legend);
        Assert.Equal(300, presentation.HeightPx);

        var transform = CardChromeResolver.ResolveSeriesTransform(card, library);
        Assert.NotNull(transform);
        Assert.Equal(5, transform!.Max);
        Assert.Equal("Other", transform.OtherLabel);
    }

    [Fact]
    public void SpecLibrary_loads_presentation_and_transform_sections()
    {
        var library = SpecLibrary.Parse(
        [
            "# presets",
            "[presentation.line_bottom_300]",
            "legend = \"bottom\"",
            "height = \"300\"",
            "",
            "[transform.series.top5]",
            "max = 5",
            "other = \"Other\"",
        ]);

        Assert.Equal("bottom", library.TryGetPresentation("line_bottom_300")!["legend"]);
        Assert.Equal(5, library.TryGetSeriesTransform("top5")!.Max);
    }

    [Fact]
    public void SpecLibrary_loads_diagram_presets()
    {
        var library = SpecLibrary.Parse(
        [
            "[diagram.demo_peak_line]",
            "kind = \"line\"",
            "render = \"chartjs\"",
            "presentation = \"line_bottom_300\"",
            "\"transform.series\" = \"top5\"",
            "x = \"usage_date\"",
            "y = \"peak\"",
            "series = \"app_name\"",
        ]);

        var preset = library.TryGetDiagram("demo_peak_line");
        Assert.NotNull(preset);
        Assert.Equal("line", preset!.Kind);
        Assert.Equal("chartjs", preset.Render);
        Assert.Equal("line_bottom_300", preset.PresentationPreset);
        Assert.Equal("top5", preset.SeriesTransformPreset);
        Assert.Equal("usage_date", preset.Properties["x"]);
    }

    [Fact]
    public void Parse_diagram_library_preset_reference()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              card c as "C" {
                diagram demo_peak_line
                datasource view dbo.t
              }
            }
            """);

        var card = doc.Cards[0];
        Assert.Equal("demo_peak_line", card.Diagram.UsePreset);
        Assert.Empty(card.Diagram.Kind);
        Assert.Empty(card.Diagram.Properties);
    }

    [Fact]
    public void CardDiagramResolver_merges_diagram_preset()
    {
        var library = SpecLibrary.Parse(
        [
            "[presentation.line_bottom_300]",
            "legend = \"bottom\"",
            "height = \"300\"",
            "[transform.series.top5]",
            "max = 5",
            "other = \"Other\"",
            "[diagram.demo_peak_line]",
            "kind = \"line\"",
            "presentation = \"line_bottom_300\"",
            "\"transform.series\" = \"top5\"",
            "x = \"usage_date\"",
            "y = \"peak\"",
            "series = \"app_name\"",
        ]);

        var card = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              card c as "C" {
                diagram demo_peak_line
                datasource view dbo.t
              }
            }
            """).Cards[0];

        var resolved = CardDiagramResolver.Resolve(card, library);
        Assert.Equal("line", resolved.Card.Diagram.Kind);
        Assert.Equal("usage_date", resolved.Card.Diagram.Properties["x"]);
        Assert.Equal("line_bottom_300", resolved.Card.Presentation!.UsePreset);
        Assert.Equal("top5", resolved.Card.SeriesTransform!.UsePreset);

        var presentation = CardChromeResolver.ResolveChartPresentation(resolved.Card, library);
        Assert.Equal(300, presentation.HeightPx);
        Assert.Equal(5, CardChromeResolver.ResolveSeriesTransform(resolved.Card, library)!.Max);
    }

    [Fact]
    public void ReadDashboardHeader_reads_id_and_title()
    {
        const string text = """
            @config "cfg.toml"

            @dashboard soak_id
            dashboard "My **Title**" {
              card a as "A" {
                diagram number { value = x }
                datasource view dbo.t
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
        Assert.Equal("demo-diagram-library.toml", doc.DiagramLibraryPath);
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
    }

    [Fact]
    public void Parse_layout_and_place()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              layout grid { columns = 12 gap = 8 }
              card a as "A" {
                place { row = 1 col = 1 span = half }
                diagram number { value = x }
                datasource view dbo.t
              }
            }
            """);

        Assert.Equal(8, doc.Layout.GapPx);
        Assert.Equal(6, doc.Cards[0].Placement?.Span);
        Assert.Equal(1, doc.Cards[0].Placement?.Row);
    }

    [Fact]
    public void Parse_filter_top_as_on_declaration()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter top events_top as "Строк (TOP)" {
                default = 200
              }
            }
            """);

        var filter = doc.Filters.Single();
        Assert.Equal("Строк (TOP)", filter.Label);
        Assert.Equal("200", filter.DefaultExpression);
    }

    [Fact]
    public void Parse_filter_column_as_label()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date {
                column = usage_date as "Дата отчёта"
                default = -7d..today
              }
            }
            """);

        var filter = doc.Filters.Single();
        Assert.Equal("usage_date", filter.ColumnReference);
        Assert.Equal("Дата отчёта", filter.Label);
    }

    [Fact]
    public void Parse_filter_default_does_not_swallow_label_on_same_line()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date {
                column = usage_date as "Daily" default = -7d..today
              }
            }
            """);

        var filter = doc.Filters.Single();
        Assert.Equal("-7d..today", filter.DefaultExpression);
        Assert.Equal("Daily", filter.Label);
        Assert.Equal("usage_date", filter.ColumnReference);
    }

    [Fact]
    public void Parse_filter_block_multiline()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date activity_range {
                column = bucket_start_utc as "Activity 5-min"
                default = -1d..today
              }
            }
            """);

        var filter = doc.Filters.Single();
        Assert.Equal("-1d..today", filter.DefaultExpression);
        Assert.Equal("Activity 5-min", filter.Label);
    }

    [Fact]
    public void Parse_bind_block_syntax()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter field app_name { column = app_name as "App" }
              filters dashboard { app_name }
              card a as "A" {
                bind { app_name }
                diagram number { value = x }
                datasource view dbo.t
              }
            }
            """);

        Assert.Equal(["app_name"], doc.Cards[0].BoundFilters);
    }

    [Fact]
    public void Parse_card_local_filters_placement()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
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
                @dashboard t
                dashboard "T" {
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
                """));

        Assert.Contains("toolbar", ex.Message);
    }

    [Fact]
    public void Parse_filters_chrome_and_tabs()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
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
            @dashboard t
            dashboard "T" {
              filters chrome { sticky = line }
            }
            """);
        Assert.True(line.FiltersChrome.IsStickyLine);

        var card = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filters chrome { sticky = card }
            }
            """);
        Assert.True(card.FiltersChrome.IsStickyCard);

        var none = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filters chrome { sticky = false }
            }
            """);
        Assert.False(none.FiltersChrome.IsSticky);
        Assert.Equal(FiltersChromeDefinition.StickyNone, none.FiltersChrome.Sticky);
    }

    [Fact]
    public void Parse_heatmap_diagram_kind()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
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
            """);

        Assert.Equal("heatmap", doc.Cards[0].Diagram.Kind);
        Assert.Equal(DiagramDataFamily.Matrix, DiagramKindRegistry.Resolve("heatmap").DataFamily);
    }

    [Fact]
    public void Parse_heatmap_column_as_labels()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
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
    public void Parse_heatmap_allows_extension_presentation_properties()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
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
            @dashboard t
            dashboard "T" {
              card h as "H" {
                diagram heatmap { x = a y = b value = c }
                legend {
                  min = "мин. {min}"
                  max = "макс. {max}"
                }
                datasource view dbo.t
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
                @dashboard t
                dashboard "T" {
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
                """));

        Assert.Contains("'where' is no longer used", ex.Message);
    }

    [Fact]
    public void Compile_bound_top_filter_does_not_add_where_clause()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date {
                column = usage_date as "Дата"
                default = -7d..today
              }
              filter top row_limit as "Limit" { default = 100 }
              filters dashboard { usage_date }
              card events as "Events" {
                filters { row_limit }
                bind usage_date, row_limit
                diagram table { columns = id, name }
                datasource view dbo.events
              }
            }
            """);

        var card = doc.Cards[0];
        var index = DashboardBootstrap.IndexFilters(doc);
        var filters = new FilterState();
        filters.SetDate("usage_date", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7));
        filters.SetTop("row_limit", 50);

        var query = QueryCompiler.Compile(card, filters, index);

        Assert.Contains("usage_date >= @usage_date_from", query.Sql);
        Assert.DoesNotContain("row_limit", query.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("SELECT TOP 50", query.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_treats_unknown_diagram_ident_as_library_preset()
    {
        var card = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              card a as "A" {
                diagram demo_custom_heatmap { x = a y = b value = c }
                datasource view dbo.t
              }
            }
            """).Cards[0];

        Assert.Equal("demo_custom_heatmap", card.Diagram.UsePreset);
        Assert.Equal("a", card.Diagram.Properties["x"]);
        Assert.Equal("c", card.Diagram.Properties["value"]);
    }

    [Fact]
    public void CardDiagramResolver_throws_when_preset_missing()
    {
        var card = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              card a as "A" {
                diagram missing_preset
                datasource view dbo.t
              }
            }
            """).Cards[0];

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CardDiagramResolver.Resolve(card, library: null));
        Assert.Contains("missing_preset", ex.Message);
    }

    [Fact]
    public void TabLayoutCompactor_bumps_full_width_table_below_same_row_charts()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
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
            """);

        var layout = TabLayoutCompactor.Compact(doc, "s");

        Assert.Equal(1, layout["a"].Row);
        Assert.Equal(1, layout["b"].Row);
        Assert.Equal(2, layout["c"].Row);
    }

    [Fact]
    public void Parse_tab_dashspec_merges_module_cards()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-tab-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "extra.dashspec"), """
                @tab extra

                card x as "X" {
                  diagram number { value = n }
                  datasource view dbo.x
                }
                """);

            var doc = DashSpecParser.Parse("""
                @dashboard t
                dashboard "T" {
                  tab overview as "Overview" {
                    cards { a }
                  }
                  tab extra dashspec "extra.dashspec"
                  card a as "A" {
                    diagram number { value = n }
                    datasource view dbo.a
                  }
                }
                """, dir);

            Assert.Equal(2, doc.Cards.Count);
            Assert.Equal("extra", doc.Tabs[1].Id);
            Assert.Equal(["x"], doc.Tabs[1].CardIds);
            Assert.Equal("extra", doc.Cards.Single(c => c.Id == "x").TabId);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parse_tab_dashspec_requires_spec_directory()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              tab x dashspec "x.dashspec"
              card a as "A" {
                diagram number { value = n }
                datasource view dbo.a
              }
            }
            """));

        Assert.Contains("specDirectory", ex.Message);
    }

    [Fact]
    public void Parse_tab_root_standalone_document()
    {
        var doc = DashSpecParser.Parse("""
            @tab soak

            connector sqlserver
            filter date usage_date on usage_date as "Date" default -7d..today
            toolbar { usage_date }

            tab soak as "Soak title"

            card a as "A" {
              bind usage_date
              diagram number { value = n }
              datasource view dbo.a
            }
            """);

        Assert.Equal("soak", doc.Id);
        Assert.Equal("Soak title", doc.Title);
        Assert.Single(doc.Tabs);
        Assert.Equal("sqlserver", doc.ConnectorId);
        Assert.Single(doc.Cards);
    }

    [Fact]
    public void ReadDashboardHeader_reads_tab_root_id()
    {
        const string text = """
            @config "cfg.toml"
            @tab stakeholder
            connector sqlserver
            card a as "A" {
              diagram number { value = x }
              datasource view dbo.t
            }
            """;

        Assert.Equal(("stakeholder", "stakeholder"), DashSpecParser.ReadDashboardHeader(text));
    }
}

public class FilterBindingTests
{
    [Fact]
    public void MapFiltersToCards_activity_day_only_on_activity_card()
    {
        var soakPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "samples", "demo",
            "demo-soak.dashspec"));
        var specDir = Path.GetDirectoryName(soakPath)!;

        var doc = DashSpecParser.Parse(File.ReadAllText(soakPath), specDir);

        var library = SpecLibrary.LoadFile(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "samples", "demo",
            "demo-diagram-library.toml")));

        var map = FilterBinding.MapFiltersToCards(doc, library);

        Assert.Equal(
            ["peak_concurrent_proxy", "activity_5min", "dau_by_product", "peak_apps_heatmap", "idle_table"],
            map["usage_date"]);
        Assert.False(map.ContainsKey("activity_slot"));
        Assert.Equal(6, map["app_name"].Count);
        Assert.Equal(["activity_slot"], doc.Cards.Single(c => c.Id == "activity_5min").LocalFilters);
        Assert.Equal(
            ["usage_date", "user_name", "app_name", "activity_slot"],
            CardResolver.ResolveCard(
                doc.Cards.Single(c => c.Id == "activity_5min"),
                library,
                doc.DashboardFilters).BoundFilters);
        Assert.Equal(["events_top"], doc.Cards.Single(c => c.Id == "events_detail").LocalFilters);
        Assert.Equal(["idle_top"], doc.Cards.Single(c => c.Id == "idle_table").LocalFilters);
    }
}

public class QueryCompilerTests
{
    [Fact]
    public void Compile_applies_optional_date_and_field_filters()
    {
        var card = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date {
                column = usage_date as "Usage"
                default = -7d..today
              }
              filter field app_name { column = demo.v_daily_active_users.app_name as "App" }
              filters dashboard { usage_date, app_name }
              card peak as "Peak" {
                bind usage_date, app_name
                diagram line {
                  x = usage_date
                  y = peak_concurrent_proxy
                  series = app_name
                }
                datasource view demo.v_daily_peak_concurrent_proxy
              }
            }
            """).Cards[0];

        var filters = new FilterState();
        filters.SetDate("usage_date", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7));
        filters.SetField("app_name", ["Tekla Structures"]);

        var index = new Dictionary<string, Model.FilterDefinition>
        {
            ["usage_date"] = new(Model.FilterKind.Date, "usage_date", "-7d..today", "usage_date"),
            ["app_name"] = new(Model.FilterKind.Field, "app_name", null, "demo.v_daily_active_users.app_name"),
        };

        var query = QueryCompiler.Compile(card, filters, index);

        Assert.Contains("usage_date >= @usage_date_from", query.Sql);
        Assert.Contains("app_name = @app_name_0", query.Sql);
        Assert.Equal(3, query.Parameters.Count);
    }

    [Fact]
    public void Compile_sql_datasource_wraps_subquery_and_applies_filters()
    {
        var card = DashSpecParser.Parse("""
            @sqldialect tsql
            @dashboard t
            dashboard "T" {
              filter date usage_date { column = usage_date as "Дата" default = -7d..today }
              filters dashboard { usage_date }
              card top as "Top" {
                bind usage_date
                diagram bar { x = user_sam y = peak_concurrent_apps }
                datasource sql "SELECT user_sam, MAX(n) AS peak_concurrent_apps FROM t GROUP BY user_sam"
              }
            }
            """).Cards[0];

        var filters = new FilterState();
        filters.SetDate("usage_date", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7));
        var index = new Dictionary<string, Model.FilterDefinition>
        {
            ["usage_date"] = new(Model.FilterKind.Date, "usage_date", "-7d..today", "usage_date"),
        };

        var query = QueryCompiler.Compile(card, filters, index, SqlDialect.TSql);

        Assert.Contains("FROM (SELECT user_sam", query.Sql);
        Assert.Contains(") AS _dashspec_q", query.Sql);
        Assert.Contains("DATEADD(day, 1, @usage_date_to)", query.Sql);
    }

    [Fact]
    public void Compile_postgres_dialect_uses_interval_for_date_upper_bound()
    {
        var card = DashSpecParser.Parse("""
            @sqldialect postgres
            @dashboard t
            dashboard "T" {
              filter date usage_date {
                column = usage_date as "Дата"
                default = -7d..today
              }
              filters dashboard { usage_date }
              card a as "A" {
                bind usage_date
                diagram line { x = usage_date y = n }
                datasource view public.metrics
              }
            }
            """).Cards[0];

        var filters = new FilterState();
        filters.SetDate("usage_date", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7));
        var index = new Dictionary<string, Model.FilterDefinition>
        {
            ["usage_date"] = new(Model.FilterKind.Date, "usage_date", null, "usage_date"),
        };

        var query = QueryCompiler.Compile(card, filters, index, SqlDialect.Postgres);

        Assert.Contains("INTERVAL '1 day'", query.Sql);
        Assert.DoesNotContain("DATEADD", query.Sql);
    }

    [Fact]
    public void Compile_table_uses_top_limit()
    {
        var card = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              card events as "Events" {
                diagram table {
                  columns = id, name
                  limit = 100
                }
                datasource view dbo.events
              }
            }
            """).Cards[0];

        var query = QueryCompiler.Compile(card, new FilterState(), new Dictionary<string, Model.FilterDefinition>());

        Assert.StartsWith("SELECT TOP 100", query.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_table_uses_bound_top_filter()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter top row_limit as "Limit" {
                default = 250
              }
              card events as "Events" {
                filters { row_limit }
                bind row_limit
                diagram table {
                  columns = id, name
                }
                datasource view dbo.events
              }
            }
            """);

        var card = doc.Cards[0];
        var index = DashboardBootstrap.IndexFilters(doc);
        var filters = new FilterState();
        filters.SetTop("row_limit", 75);

        var query = QueryCompiler.Compile(card, filters, index);

        Assert.StartsWith("SELECT TOP 75", query.Sql, StringComparison.OrdinalIgnoreCase);
    }
}

public class DateDefaultRangeTests
{
    [Theory]
    [InlineData("-7d..today", -7, 0)]
    [InlineData("-1d..today", -1, 0)]
    [InlineData("today..today", 0, 0)]
    public void Resolve_relative_ranges(string expression, int fromOffset, int toOffset)
    {
        var today = new DateOnly(2026, 6, 24);
        var range = DateDefaultRange.Resolve(expression, today);
        Assert.Equal(today.AddDays(fromOffset), range.From);
        Assert.Equal(today.AddDays(toOffset), range.To);
    }

    [Fact]
    public void Resolve_absolute_range()
    {
        var range = DateDefaultRange.Resolve("2026-06-01..2026-06-07", new DateOnly(2026, 6, 24));
        Assert.Equal(new DateOnly(2026, 6, 1), range.From);
        Assert.Equal(new DateOnly(2026, 6, 7), range.To);
    }

    [Fact]
    public void Parse_rejects_magic_preset_names()
    {
        var ex = Assert.ThrowsAny<Exception>(() =>
            DashSpecParser.Parse("""
                @dashboard t
                dashboard "T" {
                  filter date usage_date {
                    column = usage_date as "Usage"
                    default = last_7_days
                  }
                }
                """));
        Assert.Contains("..", ex.Message);
    }
}

public class ChartDataBuilderTests
{
    [Fact]
    public void BuildHeatmap_pivots_rows_and_sorts_axes()
    {
        var diagram = new DiagramDefinition("heatmap", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "usage_date",
            ["y"] = "user_name",
            ["value"] = "peak_concurrent_apps",
            ["tooltip"] = "peak_apps",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = new DateOnly(2026, 6, 23),
                ["user_name"] = "alice",
                ["peak_concurrent_apps"] = 3,
                ["peak_apps"] = "Chrome",
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = new DateOnly(2026, 6, 25),
                ["user_name"] = "alice",
                ["peak_concurrent_apps"] = 6,
                ["peak_apps"] = "AutoCAD, Revit, Chrome",
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = new DateOnly(2026, 6, 25),
                ["user_name"] = "bob",
                ["peak_concurrent_apps"] = 10,
                ["peak_apps"] = "Tekla, AutoCAD",
            },
        ];

        var matrix = ChartDataBuilder.BuildHeatmap(rows, diagram);

        Assert.Equal(["2026-06-23", "2026-06-25"], matrix.XLabels);
        Assert.Equal(["bob", "alice"], matrix.YLabels);
        Assert.Equal(10, matrix.Cells[0][1]);
        Assert.Equal(6, matrix.Cells[1][1]);
        Assert.Equal(3, matrix.Cells[1][0]);
        Assert.Null(matrix.Cells[0][0]);
        Assert.Equal(3, matrix.Min);
        Assert.Equal(10, matrix.Max);
        Assert.Equal("AutoCAD, Revit, Chrome", matrix.Tooltips![1][1]);
    }

    [Fact]
    public void BuildHeatmap_merges_y_labels_by_y_format()
    {
        var diagram = new DiagramDefinition("heatmap", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "usage_date",
            ["y"] = "user_name",
            ["value"] = "peak_concurrent_apps",
            ["tooltip"] = "peak_apps",
            ["y_format"] = "user.short",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = new DateOnly(2026, 6, 24),
                ["user_name"] = @"CORP\LonelySoul",
                ["peak_concurrent_apps"] = 10,
                ["peak_apps"] = "AutoCAD, Revit",
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = new DateOnly(2026, 6, 24),
                ["user_name"] = "LonelySoul",
                ["peak_concurrent_apps"] = 10,
                ["peak_apps"] = "Tekla",
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = new DateOnly(2026, 6, 23),
                ["user_name"] = "LonelySoul",
                ["peak_concurrent_apps"] = 3,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["usage_date"] = new DateOnly(2026, 6, 25),
                ["user_name"] = "LonelySoul",
                ["peak_concurrent_apps"] = 6,
            },
        ];

        var matrix = ChartDataBuilder.BuildHeatmap(rows, diagram);

        Assert.Single(matrix.YLabels);
        Assert.Equal("LonelySoul", matrix.YLabels[0]);
        Assert.Equal(3, matrix.Cells[0][0]);
        Assert.Equal(10, matrix.Cells[0][1]);
        Assert.Equal(6, matrix.Cells[0][2]);
        Assert.Equal("AutoCAD, Revit, Tekla", matrix.Tooltips![0][1]);
    }
}
