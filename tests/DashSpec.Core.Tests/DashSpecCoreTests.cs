using DashSpec.Abstractions.Query;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
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
    public void ReadRuntimePath_returns_relative_toml_path()
    {
        const string text = """
            @runtime "demo.toml"

            @dashboard t
            dashboard "T" {
              card a as "A" {
                diagram number { value = x }
                datasource view dbo.t
              }
            }
            """;

        Assert.Equal("demo.toml", DashSpecParser.ReadRuntimePath(text));
        Assert.Equal("demo.toml", DashSpecParser.ReadConfigPath(text));
        Assert.Equal(("t", "T"), DashSpecParser.ReadDashboardHeader(text));
        Assert.Equal("t", DashSpecParser.Parse(text).Id);
    }

    [Fact]
    public void ReadConfigPath_accepts_deprecated_alias()
    {
        const string text = """
            @config "legacy.toml"

            @dashboard t
            dashboard "T" {
              card a as "A" {
                diagram number { value = x }
                datasource view dbo.t
              }
            }
            """;

        Assert.Equal("legacy.toml", DashSpecParser.ReadRuntimePath(text));
    }

    [Fact]
    public void ReadSqlDialect_parses_file_directive()
    {
        const string text = """
            @runtime "cfg.toml"
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
                datasource sql query "SELECT user_sam, peak FROM demo.v_x GROUP BY user_sam"
              }
            }
            """);

        var card = doc.Cards[0];
        Assert.Equal(DataSourceKind.Sql, card.DataSource.Kind);
        Assert.Equal(DataSourceSqlCarrier.Query, card.DataSource.SqlCarrier);
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
                datasource sql query "{{sqlBody.Replace("\"", "\\\"")}}"
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
                datasource sql query "SELECT title FROM t WHERE title = 'DELETE is ok'"
              }
            }
            """);

        Assert.Equal(DataSourceKind.Sql, doc.Cards[0].DataSource.Kind);
        Assert.Equal(DataSourceSqlCarrier.Query, doc.Cards[0].DataSource.SqlCarrier);
    }

    [Fact]
    public void Parse_sql_datasource_rejects_bare_string_without_query_or_file()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              card a as "A" {
                diagram bar { x = a y = b }
                datasource sql "SELECT 1"
              }
            }
            """));

        Assert.Contains("query' or 'file'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_sql_datasource_file_and_block_query()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-sql-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var sqlPath = Path.Combine(dir, "queries", "top.sql");
        Directory.CreateDirectory(Path.GetDirectoryName(sqlPath)!);
        File.WriteAllText(sqlPath, "SELECT user_sam, MAX(n) AS peak FROM t GROUP BY user_sam");

        try
        {
            var fileDoc = DashSpecParser.Parse("""
                @dashboard t
                dashboard "T" {
                  card a as "A" {
                    diagram bar { x = user_sam y = peak }
                    datasource sql file "queries/top.sql"
                  }
                }
                """, dir);

            var fileCard = fileDoc.Cards[0];
            Assert.Equal(DataSourceSqlCarrier.File, fileCard.DataSource.SqlCarrier);
            Assert.Equal("queries/top.sql", fileCard.DataSource.Value);

            var blockDoc = DashSpecParser.Parse("""
                @dashboard t
                dashboard "T" {
                  card b as "B" {
                    diagram bar { x = user_sam y = peak }
                    datasource sql {
                      from query [[
                        SELECT user_sam, COUNT(*) AS peak
                        FROM t
                        GROUP BY user_sam
                      ]]
                    }
                  }
                }
                """, dir);

            var blockCard = blockDoc.Cards[0];
            Assert.Equal(DataSourceSqlCarrier.Query, blockCard.DataSource.SqlCarrier);
            Assert.Contains("COUNT(*)", blockCard.DataSource.Value);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
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
        var library = SpecLibrary.Parse(
        [
            "[presentation.line_bottom_300]",
            "legend = \"bottom\"",
            "height = \"300\"",
            "[transform.series.top5]",
            "max = 5",
            "other = \"Other\"",
        ]);

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
    public void ChartPresentation_reads_orientation_from_diagram_and_presentation()
    {
        var library = SpecLibrary.Parse(
        [
            "[presentation.bar_horizontal_320]",
            "legend = \"bottom\"",
            "height = \"320\"",
            "orientation = \"horizontal\"",
        ]);

        var fromDiagram = ChartPresentation.FromProperties(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["orientation"] = "horizontal",
            });
        Assert.True(fromDiagram.IsHorizontal);

        var card = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              card c as "C" {
                diagram bar { x = a y = b orientation = vertical }
                presentation { use = bar_horizontal_320 }
                datasource view dbo.t
              }
            }
            """).Cards[0];

        var presentation = CardChromeResolver.ResolveChartPresentation(card, library);
        Assert.True(presentation.IsHorizontal);

        var verticalCard = card with
        {
            Presentation = new PresentationBlock(
                "bar_horizontal_320",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["orientation"] = "vertical",
                }),
        };
        var verticalPresentation = CardChromeResolver.ResolveChartPresentation(verticalCard, library);
        Assert.False(verticalPresentation.IsHorizontal);
    }

    [Fact]
    public void DiagramBindings_bar_category_value_aliases()
    {
        var diagram = new DiagramDefinition("bar", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = "app_name",
            ["value"] = "distinct_users",
        });

        Assert.Equal("app_name", DiagramBindings.Column(diagram, "x"));
        Assert.Equal("distinct_users", DiagramBindings.Column(diagram, "y"));
        Assert.Equal(["app_name", "distinct_users"], DiagramBindings.SelectedSqlColumns(diagram).OrderBy(x => x).ToList());
    }

    [Fact]
    public void ChartPresentation_reads_scale_value_from_diagram()
    {
        var presentation = ChartPresentation.FromProperties(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["scale_value"] = "integer",
            });
        Assert.Equal(ChartAxisScale.Integer, presentation.ValueAxisScale);

        var card = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              card c as "C" {
                diagram bar { category = app_name value = distinct_users scale_value = integer }
                datasource view dbo.t
              }
            }
            """).Cards[0];

        var resolved = CardChromeResolver.ResolveChartPresentation(card, null);
        Assert.Equal(ChartAxisScale.Integer, resolved.ValueAxisScale);
    }

    [Fact]
    public void BuildLineOrBar_builds_category_bar_with_category_value_bindings()
    {
        var diagram = new DiagramDefinition("bar", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = "app_name",
            ["value"] = "peak_concurrent_proxy",
            ["orientation"] = "horizontal",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["app_name"] = "Tekla Structures",
                ["peak_concurrent_proxy"] = 12d,
            },
        ];

        var card = new CardDefinition(
            "c",
            "C",
            diagram,
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var payload = ChartDataBuilder.BuildLineOrBar(rows, diagram, null, card, null);

        Assert.Equal(["Tekla Structures"], payload.Labels);
        Assert.Equal(12d, payload.Series[0].Values[0]);
    }

    [Fact]
    public void BuildLineOrBar_builds_category_bar_with_reference_bindings()
    {
        var diagram = new DiagramDefinition("bar", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = "app_name",
            ["value"] = "peak_concurrent_proxy",
            ["reference"] = "purchased_seats",
            ["reference_as"] = "Куплено",
            ["orientation"] = "horizontal",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["app_name"] = "Cursor IDE",
                ["peak_concurrent_proxy"] = 6d,
                ["purchased_seats"] = 5d,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["app_name"] = "Tekla Structures",
                ["peak_concurrent_proxy"] = 3d,
                ["purchased_seats"] = 50d,
            },
        ];

        var card = new CardDefinition(
            "c",
            "C",
            diagram,
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var payload = ChartDataBuilder.BuildLineOrBar(rows, diagram, null, card, null);

        Assert.Equal(["Cursor IDE", "Tekla Structures"], payload.Labels);
        Assert.Equal(6d, payload.Series[0].Values[0]);
        Assert.Equal(3d, payload.Series[0].Values[1]);
        Assert.NotNull(payload.ReferenceValues);
        Assert.Equal(5d, payload.ReferenceValues![0]);
        Assert.Equal(50d, payload.ReferenceValues[1]);
        Assert.Equal("Куплено", payload.ReferenceLabel);
        Assert.Equal("#ef4444", payload.Series[0].PointColors![0]);
        Assert.Contains("purchased_seats", DiagramBindings.SelectedSqlColumns(diagram));
    }

    [Fact]
    public void ChartPresentation_reads_scale_y_from_diagram()
    {
        var fromDiagram = ChartPresentation.FromProperties(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["scale_y"] = "integer",
            });
        Assert.Equal(ChartAxisScale.Integer, fromDiagram.ValueAxisScale);

        var decimalDefault = ChartPresentation.FromProperties(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        Assert.Equal(ChartAxisScale.Decimal, decimalDefault.ValueAxisScale);

        var card = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              card c as "C" {
                diagram bar { x = app_name y = distinct_users scale_y = integer }
                datasource view dbo.t
              }
            }
            """).Cards[0];

        var presentation = CardChromeResolver.ResolveChartPresentation(card, null);
        Assert.Equal(ChartAxisScale.Integer, presentation.ValueAxisScale);
    }

    [Fact]
    public void ChartPresentation_scale_x_fallback_when_scale_y_absent()
    {
        var presentation = ChartPresentation.FromProperties(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["scale_x"] = "integer",
            });
        Assert.Equal(ChartAxisScale.Integer, presentation.ValueAxisScale);
    }

    [Fact]
    public void ChartPresentation_deprecated_value_scale_and_y_format_integer_aliases()
    {
        var fromValueScale = ChartPresentation.FromProperties(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["value_scale"] = "integer",
            });
        Assert.Equal(ChartAxisScale.Integer, fromValueScale.ValueAxisScale);

        var fromYFormat = ChartPresentation.FromProperties(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["y_format"] = "integer",
            });
        Assert.Equal(ChartAxisScale.Integer, fromYFormat.ValueAxisScale);

        var labelFormatIgnored = ChartPresentation.FromProperties(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["y_format"] = "user.short",
            });
        Assert.Equal(ChartAxisScale.Decimal, labelFormatIgnored.ValueAxisScale);
    }

    [Fact]
    public void ChartOrientationParser_accepts_aliases()
    {
        Assert.Equal(ChartOrientation.Horizontal, ChartOrientationParser.Parse("barh"));
        Assert.Equal(ChartOrientation.Vertical, ChartOrientationParser.Parse("vertical"));
        Assert.Equal(ChartOrientation.Vertical, ChartOrientationParser.Parse("unknown", ChartOrientation.Vertical));
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
    public void SpecLibrary_loads_palette_sections()
    {
        var library = SpecLibrary.Parse(
        [
            "[palette.brand]",
            "colors = \"#111111,#222222\"",
            "default = \"#999999\"",
            "Tekla = \"#e11d48\"",
        ]);

        var palette = library.TryGetPalette("brand");
        Assert.NotNull(palette);
        Assert.Equal("#111111,#222222", palette!["colors"]);
        Assert.Equal("#e11d48", palette["Tekla"]);
    }

    [Fact]
    public void ChartColorResolver_maps_palette_and_series_overrides()
    {
        var library = SpecLibrary.Parse(
        [
            "[palette.brand]",
            "colors = \"#111111,#222222\"",
            "default = \"#999999\"",
            "Tekla = \"#e11d48\"",
        ]);

        var card = new CardDefinition(
            "c",
            "C",
            new DiagramDefinition("line", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["color_palette"] = "brand",
                ["series_colors"] = "AVEVA:#2563eb",
            }),
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            [],
            null,
            null,
            null,
            null);

        var series = new[]
        {
            new ChartSeries("Tekla", [1d]),
            new ChartSeries("AVEVA", [2d]),
            new ChartSeries("Other", [3d]),
            new ChartSeries("Unknown", [4d]),
        };

        var colored = ChartColorResolver.ApplySeriesColors(series, card, library);
        Assert.Equal("#e11d48", colored[0].Color);
        Assert.Equal("#2563eb", colored[1].Color);
        Assert.Equal("#999999", colored[2].Color);
        Assert.NotNull(colored[3].Color);

        var orderA = ChartColorResolver.ApplySeriesColors(
        [
            new ChartSeries("Beta", [1d]),
            new ChartSeries("Alpha", [2d]),
        ], card, library);
        var orderB = ChartColorResolver.ApplySeriesColors(
        [
            new ChartSeries("Alpha", [1d]),
            new ChartSeries("Beta", [2d]),
        ], card, library);
        Assert.Equal(
            orderA.First(x => x.Name == "Alpha").Color,
            orderB.First(x => x.Name == "Alpha").Color);
        Assert.Equal(
            orderA.First(x => x.Name == "Beta").Color,
            orderB.First(x => x.Name == "Beta").Color);
    }

    [Fact]
    public void ChartColorResolver_prefix_matches_palette_keys()
    {
        var library = SpecLibrary.Parse(
        [
            "[palette.brand]",
            "colors = \"#111111,#222222\"",
            "default = \"#999999\"",
            "Cursor = \"#8b5cf6\"",
        ]);

        var card = new CardDefinition(
            "c",
            "C",
            new DiagramDefinition("line", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["color_palette"] = "brand",
            }),
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var colored = ChartColorResolver.ApplySeriesColors(
            [new ChartSeries("Cursor IDE", [1d])],
            card,
            library);
        Assert.Equal("#8b5cf6", colored[0].Color);
    }

    [Fact]
    public void ChartColorResolver_applies_dashboard_palette_when_diagram_has_none()
    {
        var library = SpecLibrary.Parse(
        [
            "[palette.brand]",
            "colors = \"#111111,#222222\"",
            "default = \"#999999\"",
            "Tekla = \"#e11d48\"",
        ]);

        var card = new CardDefinition(
            "c",
            "C",
            new DiagramDefinition("line", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["series"] = "app_name",
            }),
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var series = new[] { new ChartSeries("Tekla", [1d]) };
        var colored = ChartColorResolver.ApplySeriesColors(series, card, library, dashboardColorPalette: "brand");
        Assert.Equal("#e11d48", colored[0].Color);
    }

    [Fact]
    public void SpecResolveExporter_includes_dashboard_palette_and_effective_card()
    {
        var library = SpecLibrary.Parse(
        [
            "[palette.lus]",
            "default = \"#999999\"",
            "Tekla = \"#e11d48\"",
            "[diagram.d1]",
            "kind = \"line\"",
            "x = \"usage_date\"",
            "y = \"peak\"",
            "[card.c1]",
            "diagram = \"d1\"",
            "datasource = \"dbo.t\"",
        ]);

        var document = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              palette lus
              card c as "C" {
                use c1
              }
            }
            """);

        var export = SpecResolveExporter.Export(document, library);
        Assert.Equal("lus", export.ColorPalette);
        Assert.Single(export.Cards);
        Assert.Equal("line", export.Cards[0].DiagramKind);
        Assert.Equal("lus", export.Cards[0].EffectiveColorPalette);
        Assert.Equal("usage_date", export.Cards[0].Diagram["x"]);
    }

    [Fact]
    public void Parse_dashboard_palette_directive()
    {
        var document = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              palette = "lus_apps"
              card c as "C" {
                diagram number { value = x }
                datasource view dbo.t
              }
            }
            """);

        Assert.Equal("lus_apps", document.ColorPalette);
    }

    [Fact]
    public void Parse_palette_file_directive()
    {
        var document = DashSpecParser.Parse("""
            @palette "palettes/brand.dashpalette"
            @dashboard t
            dashboard "T" {
              palette brand
              card c as "C" {
                diagram number { value = x }
                datasource view dbo.t
              }
            }
            """);

        Assert.Equal("palettes/brand.dashpalette", document.PalettePath);
        Assert.Equal("brand", document.ColorPalette);
    }

    [Fact]
    public void PaletteModuleParser_loads_quoted_series_keys()
    {
        var library = PaletteModuleParser.LoadPaletteFile(WriteTempPalette("""
            @palette lus_apps

            palette {
              colors = "#111111,#222222"
              default = "#999999"
              Tekla = "#e11d48"
              "Cursor IDE" = "#8b5cf6"
            }
            """));

        var palette = library.TryGetPalette("lus_apps");
        Assert.NotNull(palette);
        Assert.Equal("#111111,#222222", palette!["colors"]);
        Assert.Equal("#e11d48", palette["Tekla"]);
        Assert.Equal("#8b5cf6", palette["Cursor IDE"]);
    }

    [Fact]
    public void PaletteModuleParser_resolves_const_refs_css_names_and_color_list()
    {
        var library = PaletteModuleParser.LoadPaletteFile(WriteTempPalette("""
            @palette brand

            const default = "#999999"
            const tekla = "#e11d48"
            const accent = blue

            palette {
              default = default
              Tekla = tekla
              Other = default
              colors = [tekla, accent, green, orange]
            }
            """));

        var palette = library.TryGetPalette("brand");
        Assert.NotNull(palette);
        Assert.Equal("#999999", palette!["default"]);
        Assert.Equal("#e11d48", palette["Tekla"]);
        Assert.Equal("#e11d48,#0000ff,#008000,#ffa500", palette["colors"]);
    }

    [Fact]
    public void PaletteModuleParser_keeps_legacy_colors_string()
    {
        var library = PaletteModuleParser.LoadPaletteFile(WriteTempPalette("""
            @palette legacy
            palette {
              colors = "#111111,#222222"
              default = "#999999"
            }
            """));

        Assert.Equal("#111111,#222222", library.TryGetPalette("legacy")!["colors"]);
    }

    [Fact]
    public void SpecLibraryComposer_merges_palette_with_diagram_library()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-palette-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var specPath = Path.Combine(dir, "t.dashspec");
        File.WriteAllText(specPath, "@dashboard t\ndashboard \"T\" { }");
        File.WriteAllText(Path.Combine(dir, "lib.toml"), "[diagram.d1]\nkind = \"line\"\n");
        File.WriteAllText(Path.Combine(dir, "brand.dashpalette"), """
            @palette brand
            palette { default = "#999999" }
            """);

        var library = SpecLibraryComposer.Load(specPath, "lib.toml", "brand.dashpalette");
        Assert.NotNull(library);
        Assert.NotNull(library!.TryGetDiagram("d1"));
        Assert.Equal("#999999", library.TryGetPalette("brand")!["default"]);
    }

    [Fact]
    public void Parse_lus_palette_specs_when_repo_present()
    {
        var dir = @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec";
        var soakPath = Path.Combine(dir, "lus-dev-soak.dashspec");
        if (!File.Exists(soakPath))
        {
            return;
        }

        var soak = DashSpecParser.Parse(File.ReadAllText(soakPath), dir);
        Assert.Equal("palettes/lus-apps.dashpalette", soak.PalettePath);
        Assert.Equal("lus_apps", soak.ColorPalette);

        var library = SpecLibraryComposer.Load(soakPath, soak.DiagramLibraryPath, soak.PalettePath, dir);
        Assert.Equal("#e11d48", library!.TryGetPalette("lus_apps")!["Tekla"]);

        var stakePath = Path.Combine(dir, "lus-dev-stakeholder.dashspec");
        var stake = DashSpecParser.Parse(File.ReadAllText(stakePath), dir);
        Assert.Equal("lus_apps", stake.ColorPalette);
    }

    private static string WriteTempPalette(string text)
    {
        var path = Path.Combine(Path.GetTempPath(), "dashpalette-" + Guid.NewGuid().ToString("N") + ".dashpalette");
        File.WriteAllText(path, text);
        return path;
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
            @runtime "cfg.toml"

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
    public void Parse_bar_reference_column_as_label()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              card peak as "Peak" {
                diagram bar {
                  category = app_name
                  value = peak_concurrent_proxy
                  reference = purchased_seats as "Куплено"
                }
                datasource view dbo.t
              }
            }
            """);

        var diagram = doc.Cards[0].Diagram;
        Assert.Equal("purchased_seats", diagram.Properties["reference"]);
        Assert.Equal("Куплено", diagram.Properties["reference_as"]);
    }

    [Fact]
    public void Parse_date_filter_inline_widget_day_and_grain_filter()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date period_start on period_start as "Период" default today widget day grain_filter period_grain
              filter date activity_slot on bucket_start_utc as "День" default today widget day
              card c as "C" {
                diagram table { columns = a }
                datasource view dbo.t
              }
            }
            """);

        var period = doc.Filters.Single(f => f.Name == "period_start");
        Assert.Equal("Период", period.Label);
        Assert.Equal("day", period.Widget);
        Assert.Equal("period_grain", period.GrainFilterName);
        Assert.Equal("today..today", period.DefaultExpression);

        var slot = doc.Filters.Single(f => f.Name == "activity_slot");
        Assert.Equal("bucket_start_utc", slot.ColumnReference);
        Assert.Equal("День", slot.Label);
    }

    [Fact]
    public void Parse_date_filter_inline_range_without_widget_does_not_bleed_into_next_line()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date activity_slot on bucket_start_utc as "День" default today..today
              filter date period_start on period_start as "Период" default today widget day grain_filter period_grain
              card c as "C" {
                diagram table { columns = a }
                datasource view dbo.t
              }
            }
            """);

        Assert.Equal(2, doc.Filters.Count);
        Assert.Equal("today..today", doc.Filters.Single(f => f.Name == "activity_slot").DefaultExpression);
        Assert.Null(doc.Filters.Single(f => f.Name == "activity_slot").Widget);
    }

    [Fact]
    public void Compile_day_widget_uses_half_open_day_range_not_equality()
    {
        var card = new CardDefinition(
            "activity",
            "Activity",
            new DiagramDefinition("bar", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x"] = "bucket_start_utc",
                ["y"] = "event_count",
            }),
            new DataSourceDefinition(DataSourceKind.View, "lus.v_hourly_activity"),
            ["activity_slot"],
            []);

        var filters = new FilterState();
        filters.SetDate("activity_slot", new DateOnly(2026, 6, 30), new DateOnly(2026, 6, 30));

        var filterIndex = new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["activity_slot"] = new(
                FilterKind.Date,
                "activity_slot",
                "today..today",
                "bucket_start_utc",
                Widget: "day"),
        };

        var query = QueryCompiler.Compile(card, filters, filterIndex, SqlDialect.TSql);

        Assert.Contains("bucket_start_utc >= @activity_slot_from", query.Sql);
        Assert.Contains("bucket_start_utc < DATEADD(day, 1, @activity_slot_to)", query.Sql);
        Assert.DoesNotContain("@activity_slot_day", query.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_card_include_diagram_file_and_stdlib_presentation()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-include-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "diagrams"));
        var stdlib = Path.Combine(dir, "stdlib", "presentation");
        Directory.CreateDirectory(stdlib);
        try
        {
            File.WriteAllText(Path.Combine(stdlib, "heatmap_tall.dashpresentation"), """
                @presentation heatmap_tall

                presentation {
                  height = 420
                }
                """);

            File.WriteAllText(Path.Combine(dir, "diagrams", "activity.dashdiagram"), """
                @diagram activity

                include presentation "<presentation/heatmap_tall>"

                diagram heatmap {
                  x = bucket_start_utc
                  y = app_name
                  value = event_count
                  x_format = time.short
                  x_step = 1h
                }
                """);

            SpecIncludeResolver.SetStdlibRootForTests(Path.Combine(dir, "stdlib"));

            var doc = DashSpecParser.Parse("""
                @dashboard t
                dashboard "T" {
                  card c as "C" {
                    include diagram "diagrams/activity.dashdiagram"
                    datasource view dbo.t
                  }
                }
                """, dir);

            var card = doc.Cards.Single();
            Assert.Equal("heatmap", card.Diagram.Kind);
            Assert.Equal("bucket_start_utc", card.Diagram.Properties["x"]);
            Assert.Equal("420", card.Presentation!.Properties["height"]);
        }
        finally
        {
            SpecIncludeResolver.SetStdlibRootForTests(null);
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parse_field_filter_single_select_combobox()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter field period_grain on demo.v_peak.period_grain as "Grain" default day widget combobox single
              card c as "C" {
                diagram table { columns = a }
                datasource view dbo.t
              }
            }
            """);

        var grain = doc.Filters.Single(f => f.Name == "period_grain");
        Assert.Equal("combobox", grain.Widget);
        Assert.True(grain.SingleSelect);
        Assert.True(grain.IsSingleSelectField);
        Assert.Equal("day", grain.DefaultExpression);
    }

    [Fact]
    public void ResolveChartPresentation_reads_category_value_axis_labels_from_bar_diagram()
    {
        var card = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              card peak as "Peak" {
                diagram bar {
                  category = app_name as "Продукт"
                  value = peak_concurrent_proxy as "Пик (proxy)"
                  orientation = horizontal
                }
                datasource view dbo.t
              }
            }
            """).Cards[0];

        var presentation = CardChromeResolver.ResolveChartPresentation(card, null);
        Assert.Equal("Продукт", presentation.CategoryAxisLabel);
        Assert.Equal("Пик (proxy)", presentation.ValueAxisLabel);
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
    public void Parse_tab_dashspec_ignores_module_shell_filters_when_embedded()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-tab-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "extra.dashspec"), """
                @tab extra

                filter field app_name on dbo.apps.name as "Apps"

                tab extra as "Extra" {
                  filter top n as "Top" default 5
                }

                card x as "X" {
                  diagram number { value = n }
                  datasource view dbo.x
                }
                """);

            var doc = DashSpecParser.Parse("""
                @dashboard t
                dashboard "T" {
                  filter field app_name on dbo.apps.name as "Apps"
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

            Assert.Single(doc.Filters, f => f.Name == "app_name");
            Assert.Single(doc.Filters, f => f.Name == "n");
            Assert.Equal(2, doc.Cards.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
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
            @runtime "cfg.toml"
            @tab stakeholder
            connector sqlserver
            card a as "A" {
              diagram number { value = x }
              datasource view dbo.t
            }
            """;

        Assert.Equal(("stakeholder", "stakeholder"), DashSpecParser.ReadDashboardHeader(text));
    }

    [Fact]
    public void Parse_card_ref_and_tab_layout_board()
    {
        var doc = DashSpecParser.Parse("""
            @tab demo

            tab demo as "Demo" {
              layout {
                [ Q E ]
                [ T F ]
              }
            }

            card peak_by_app as "Peak" ref Q {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card peak_apps as "Apps" ref E {
              diagram heatmap { x = a y = b value = c }
              datasource view dbo.t
            }
            card idle as "Idle" ref T {
              diagram heatmap { x = a y = b value = c }
              datasource view dbo.t
            }
            card utilization as "Util" ref F {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            """);

        Assert.Equal("Q", doc.Cards[0].LayoutRef);
        Assert.NotNull(doc.Tabs[0].LayoutBoard);
        Assert.Equal(2, doc.Tabs[0].LayoutBoard!.RowCount);
        Assert.Equal(2, doc.Tabs[0].LayoutBoard.ColumnCount);
    }

    [Fact]
    public void TabLayoutBoardResolver_places_2x2_grid()
    {
        var doc = DashSpecParser.Parse("""
            @tab demo

            tab demo {
              layout {
                [ Q E ]
                [ T F ]
              }
            }

            card a as "A" ref Q {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card b as "B" ref E {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card c as "C" ref T {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card d as "D" ref F {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            """);

        var layout = TabLayoutCompactor.Compact(doc, "demo");

        Assert.Equal(new PlacementDefinition(1, 1, 6), layout["a"]);
        Assert.Equal(new PlacementDefinition(1, 7, 6), layout["b"]);
        Assert.Equal(new PlacementDefinition(2, 1, 6), layout["c"]);
        Assert.Equal(new PlacementDefinition(2, 7, 6), layout["d"]);
    }

    [Fact]
    public void TabLayoutBoardResolver_single_cell_row_is_full_width()
    {
        var doc = DashSpecParser.Parse("""
            @tab demo

            tab demo {
              layout {
                [ Q W ]
                [ E ]
              }
            }

            card a as "A" ref Q {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card b as "B" ref W {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card c as "C" ref E {
              diagram heatmap { x = a y = b value = c }
              datasource view dbo.t
            }
            """);

        var layout = TabLayoutCompactor.Compact(doc, "demo");

        Assert.Equal(6, layout["a"].Span);
        Assert.Equal(6, layout["b"].Span);
        Assert.Equal(12, layout["c"].Span);
        Assert.Equal(2, layout["c"].Row);
    }

    [Fact]
    public void TabLayoutBoardResolver_uneven_rows_distribute_per_row()
    {
        var doc = DashSpecParser.Parse("""
            @tab demo

            tab demo {
              layout {
                [ Q E ]
                [ R T Y ]
                [ F ]
              }
            }

            card q as "Q" ref Q {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card e as "E" ref E {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card r as "R" ref R {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card t as "T" ref T {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card y as "Y" ref Y {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            card f as "F" ref F {
              diagram bar { x = a y = b }
              datasource view dbo.t
            }
            """);

        Assert.Equal(3, doc.Tabs[0].LayoutBoard!.RowCount);
        Assert.Equal(3, doc.Tabs[0].LayoutBoard.ColumnCount);

        var layout = TabLayoutCompactor.Compact(doc, "demo");

        Assert.Equal(new PlacementDefinition(1, 1, 6), layout["q"]);
        Assert.Equal(new PlacementDefinition(1, 7, 6), layout["e"]);
        Assert.Equal(new PlacementDefinition(2, 1, 4), layout["r"]);
        Assert.Equal(new PlacementDefinition(2, 5, 4), layout["t"]);
        Assert.Equal(new PlacementDefinition(2, 9, 4), layout["y"]);
        Assert.Equal(new PlacementDefinition(3, 1, 12), layout["f"]);
    }

    [Fact]
    public void Parse_include_layout_at_tab_module_shell()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "layouts"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "layouts", "grid.dashlayout"), """
                @layout g

                [ Q E ]
                [ T F ]
                """);

            var doc = DashSpecParser.Parse("""
                @tab demo

                include layout "layouts/grid.dashlayout"

                card a as "A" ref Q {
                  diagram bar { x = a y = b }
                  datasource view dbo.t
                }
                card b as "B" ref E {
                  diagram bar { x = a y = b }
                  datasource view dbo.t
                }
                card c as "C" ref T {
                  diagram bar { x = a y = b }
                  datasource view dbo.t
                }
                card d as "D" ref F {
                  diagram bar { x = a y = b }
                  datasource view dbo.t
                }
                """, dir);

            Assert.NotNull(doc.Tabs[0].LayoutBoard);
            Assert.Equal(2, doc.Tabs[0].LayoutBoard!.RowCount);
            var layout = TabLayoutCompactor.Compact(doc, "demo");
            Assert.Equal(6, layout["a"].Span);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parse_include_layout_conflicts_with_inline_tab_layout()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "layouts"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "layouts", "grid.dashlayout"), """
                @layout g
                [ Q ]
                """);

            var ex = Assert.Throws<DashSpecParseException>(() => DashSpecParser.Parse("""
                @tab demo

                include layout "layouts/grid.dashlayout"

                tab demo {
                  layout { [ Q ] }
                }

                card a as "A" ref Q {
                  diagram bar { x = a y = b }
                  datasource view dbo.t
                }
                """, dir));

            Assert.Contains("twice", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
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

        SpecLibrary? library = null;

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
                datasource sql query "SELECT user_sam, MAX(n) AS peak_concurrent_apps FROM t GROUP BY user_sam"
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

    [Fact]
    public void BuildHeatmap_formats_x_axis_with_time_short()
    {
        var diagram = new DiagramDefinition("heatmap", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "bucket_start_utc",
            ["y"] = "app_name",
            ["value"] = "event_count",
            ["x_format"] = "time.short",
            ["y_format"] = "raw",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket_start_utc"] = new DateTime(2026, 6, 30, 14, 0, 0),
                ["app_name"] = "Cursor IDE",
                ["event_count"] = 120d,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket_start_utc"] = new DateTime(2026, 6, 30, 8, 0, 0),
                ["app_name"] = "Cursor IDE",
                ["event_count"] = 40d,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket_start_utc"] = new DateTime(2026, 6, 30, 14, 0, 0),
                ["app_name"] = "Google Chrome",
                ["event_count"] = 15d,
            },
        ];

        var matrix = ChartDataBuilder.BuildHeatmap(rows, diagram);

        Assert.Equal(["08:00", "14:00"], matrix.XLabels);
        Assert.Equal(["Cursor IDE", "Google Chrome"], matrix.YLabels);
        Assert.Equal(40, matrix.Cells[0][0]);
        Assert.Equal(120, matrix.Cells[0][1]);
        Assert.Equal(15, matrix.Cells[1][1]);
    }

    [Fact]
    public void BuildHeatmap_with_x_step_fills_full_day_hour_grid()
    {
        var diagram = new DiagramDefinition("heatmap", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "bucket_start_utc",
            ["y"] = "app_name",
            ["value"] = "event_count",
            ["x_format"] = "time.short",
            ["x_step"] = "1h",
            ["y_format"] = "raw",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket_start_utc"] = new DateTime(2026, 6, 30, 0, 0, 0),
                ["app_name"] = "Cursor IDE",
                ["event_count"] = 81d,
            },
        ];

        var matrix = ChartDataBuilder.BuildHeatmap(rows, diagram);

        Assert.Equal(24, matrix.XLabels.Count);
        Assert.Equal("00:00", matrix.XLabels[0]);
        Assert.Equal("23:00", matrix.XLabels[23]);
        Assert.Equal(81, matrix.Cells[0][0]);
        Assert.Null(matrix.Cells[0][1]);
    }

    [Fact]
    public void BuildLineOrBar_fills_five_minute_grid_when_x_step_set()
    {
        var diagram = new DiagramDefinition("line", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "bucket",
            ["y"] = "n",
            ["x_step"] = "5m",
            ["x_format"] = "time.short",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket"] = new DateTime(2026, 6, 24, 7, 40, 0),
                ["n"] = 1d,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket"] = new DateTime(2026, 6, 24, 8, 5, 0),
                ["n"] = 2d,
            },
        ];

        var card = new CardDefinition(
            "c",
            "C",
            diagram,
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);
        var payload = ChartDataBuilder.BuildLineOrBar(rows, diagram, null, card, null);

        Assert.Equal(6, payload.Labels.Count);
        Assert.Equal("07:40", payload.Labels[0]);
        Assert.Equal("07:45", payload.Labels[1]);
        Assert.Equal("08:00", payload.Labels[4]);
        Assert.Equal("08:05", payload.Labels[5]);
        Assert.Equal(1d, payload.Series[0].Values[0]);
        Assert.Null(payload.Series[0].Values[1]);
        Assert.Equal(2d, payload.Series[0].Values[5]);
    }

    [Fact]
    public void BuildLineOrBar_honors_arbitrary_x_step_from_spec()
    {
        var diagram = new DiagramDefinition("line", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "bucket",
            ["y"] = "n",
            ["x_step"] = "10m",
            ["x_format"] = "time.short",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket"] = new DateTime(2026, 6, 24, 7, 0, 0),
                ["n"] = 1d,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["bucket"] = new DateTime(2026, 6, 24, 7, 25, 0),
                ["n"] = 2d,
            },
        ];

        var card = new CardDefinition(
            "c",
            "C",
            diagram,
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var payload = ChartDataBuilder.BuildLineOrBar(rows, diagram, null, card, null);

        Assert.Equal(["07:00", "07:10", "07:20"], payload.Labels);
        Assert.Equal(2d, payload.Series[0].Values[2]);
    }

    [Fact]
    public void BuildLineOrBar_builds_category_bar_for_app_name_axis()
    {
        var diagram = new DiagramDefinition("bar", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["x"] = "app_name",
            ["y"] = "peak_concurrent_proxy",
            ["orientation"] = "horizontal",
        });

        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["app_name"] = "Tekla Structures",
                ["peak_concurrent_proxy"] = 12d,
            },
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["app_name"] = "AutoCAD",
                ["peak_concurrent_proxy"] = 8d,
            },
        ];

        var card = new CardDefinition(
            "c",
            "C",
            diagram,
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            []);

        var payload = ChartDataBuilder.BuildLineOrBar(rows, diagram, null, card, null);

        Assert.Equal(["Tekla Structures", "AutoCAD"], payload.Labels);
        Assert.Equal(12d, payload.Series[0].Values[0]);
        Assert.Equal(8d, payload.Series[0].Values[1]);
        Assert.NotNull(payload.Series[0].PointColors);
        Assert.Equal(2, payload.Series[0].PointColors!.Count);
    }

    [Fact]
    public void Compile_period_start_with_grain_filter_uses_period_anchor()
    {
        var card = new CardDefinition(
            "peak",
            "Peak",
            new DiagramDefinition("bar", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x"] = "app_name",
                ["y"] = "peak_concurrent_proxy",
            }),
            new DataSourceDefinition(DataSourceKind.View, "lus.v_peak_concurrent_by_period"),
            ["period_grain", "period_start", "app_name"],
            []);

        var filters = new FilterState();
        filters.SetField("period_grain", ["month"]);
        filters.SetDate("period_start", new DateOnly(2026, 6, 24), new DateOnly(2026, 6, 24));

        var filterIndex = new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["period_grain"] = new(FilterKind.Field, "period_grain", "day", "lus.v_peak.period_grain"),
            ["period_start"] = new(
                FilterKind.Date,
                "period_start",
                "today..today",
                "period_start",
                GrainFilterName: "period_grain"),
            ["app_name"] = new(FilterKind.Field, "app_name", null, "lus.v_peak.app_name"),
        };

        var query = QueryCompiler.Compile(card, filters, filterIndex);

        Assert.Contains("period_start = @period_start_anchor", query.Sql);
        Assert.Contains("period_grain = @period_grain_0", query.Sql);
        Assert.Equal(new DateOnly(2026, 6, 1), query.Parameters.Single(p => p.Name == "@period_start_anchor").Value);
    }

    [Fact]
    public void Parse_filter_ref_does_not_consume_next_line_filter()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter field period_grain on demo.v_peak.period_grain as "Grain"
              filter top events_top as "Строк (TOP)" default 200
            }
            """);

        Assert.Equal(2, doc.Filters.Count);
        Assert.Equal("events_top", doc.Filters[1].Name);
    }

    [Fact]
    public void Parse_filter_ref_and_toolbar_layout_board()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date on usage_date as "Date" ref D default -7d..today
              filter field app_name on dbo.t.app as "App" ref A widget combobox
              filter field user_name on dbo.t.user as "User" ref U widget combobox
              toolbar {
                [ D A ]
                [ U ]
              }
              card c as "C" {
                bind usage_date
                diagram number { value = n }
                datasource view dbo.t
              }
            }
            """);

        Assert.Equal("D", doc.Filters[0].LayoutRef);
        Assert.NotNull(doc.ToolbarBoard);
        Assert.Equal(2, doc.ToolbarBoard!.RowCount);
        Assert.Equal(["usage_date", "app_name", "user_name"], doc.DashboardFilters);
    }

    [Fact]
    public void ToolbarLayoutCompactor_places_board_on_grid()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              layout grid { columns = 12 }
              filter date d1 on c1 as "D1" ref D default -7d..today
              filter field f1 on c2 as "F1" ref A widget combobox
              filter field f2 on c3 as "F2" ref U widget combobox
              toolbar {
                [ D A ]
                [ U ]
              }
              card c as "C" {
                bind d1
                diagram number { value = n }
                datasource view dbo.t
              }
            }
            """);

        var layout = ToolbarLayoutCompactor.Compact(doc);

        Assert.Equal(new PlacementDefinition(1, 1, 6), layout["d1"]);
        Assert.Equal(new PlacementDefinition(1, 7, 6), layout["f1"]);
        Assert.Equal(new PlacementDefinition(2, 1, 12), layout["f2"]);
    }

    [Fact]
    public void Parse_include_toolbar_dashlayout()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-toolbar-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        Directory.CreateDirectory(Path.Combine(dir, "layouts"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "layouts", "tb.dashlayout"), """
                @layout tb

                [ D A ]
                [ U ]
                """);
            File.WriteAllText(Path.Combine(dir, "root.dashspec"), """
                @dashboard t
                dashboard "T" {
                  include toolbar "layouts/tb.dashlayout"
                  filter date d1 on c1 as "D1" ref D default -7d..today
                  filter field f1 on c2 as "F1" ref A widget combobox
                  filter field f2 on c3 as "F2" ref U widget combobox
                  card c as "C" {
                    bind d1
                    diagram number { value = n }
                    datasource view dbo.t
                  }
                }
                """);

            var doc = DashSpecParser.Parse(File.ReadAllText(Path.Combine(dir, "root.dashspec")), dir);

            Assert.NotNull(doc.ToolbarBoard);
            Assert.Equal(["d1", "f1", "f2"], doc.DashboardFilters);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parse_toolbar_board_rejects_flat_list_combo()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date d1 on c1 as "D1" ref D default -7d..today
              toolbar { d1 }
              toolbar { [ D ] }
              card c as "C" {
                bind d1
                diagram number { value = n }
                datasource view dbo.t
              }
            }
            """));

        Assert.Contains("cannot combine a layout board with a flat filter list", ex.Message);
    }
}
