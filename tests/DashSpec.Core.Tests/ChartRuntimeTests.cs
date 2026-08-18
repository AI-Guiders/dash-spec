using DashSpec.Abstractions.Query;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class ChartRuntimeTests
{
    [Fact]
    public void Parse_presentation_and_transform_blocks()
    {
        var doc = DashSpecParser.Parse("""

            @dashboard t
              configuration
              diagramlibrary = "lib.toml"
              end configuration
              report
              title = "T"
              card c as "C"
              diagram line
              x = usage_date
              y = value
              series = app_name
              end line
              presentation
              use = line_bottom
              legend = right
              end presentation
              transform series
              other = "Прочее"
              max = 4
              end transform
              datasource view dbo.t
              end card
              end report
            end dashboard
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
              report
              title = "T"
              card c as "C"
              diagram line
              x = a y
              series = s
              end line
              presentation
              use = line_bottom_300
              end presentation
              transform series
              use = top5
              end transform
              datasource view dbo.t
              end card
              end report
            end dashboard
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
              report
              title = "T"
              card c as "C"
              diagram bar
              x = a y
              orientation = vertical
              end bar
              presentation
              use = bar_horizontal_320
              end presentation
              datasource view dbo.t
              end card
              end report
            end dashboard
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
              report
              title = "T"
              card c as "C"
              diagram bar
              category = app_name value
              scale_value = integer
              end bar
              datasource view dbo.t
              end card
              end report
            end dashboard
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
        Assert.Equal("#60a5fa", payload.Series[0].PointColors![0]);
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
              report
              title = "T"
              card c as "C"
              diagram bar
              x = app_name y
              scale_y = integer
              end bar
              datasource view dbo.t
              end card
              end report
            end dashboard
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
    public void ResolveChartPresentation_reads_category_value_axis_labels_from_bar_diagram()
    {
        var card = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              card peak as "Peak"
              diagram bar
              category = app_name as "Продукт"
              value = peak_concurrent_proxy as "Пик (proxy)"
              orientation = horizontal
              end bar
              datasource view dbo.t
              end card
              end report
            end dashboard
""").Cards[0];

        var presentation = CardChromeResolver.ResolveChartPresentation(card, null);
        Assert.Equal("Продукт", presentation.CategoryAxisLabel);
        Assert.Equal("Пик (proxy)", presentation.ValueAxisLabel);
    }

    [Fact]
    public void ChartPresentation_percent_scale_defaults_axis_max_to_100()
    {
        var presentation = ChartPresentation.FromProperties(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["scale_value"] = "percent",
            });

        Assert.Equal(ChartAxisScale.Percent, presentation.ValueAxisScale);
        Assert.Equal(100, presentation.ValueAxisMax);
    }

    [Fact]
    public void ResolveChartPresentation_merges_nested_presentation_y_max_for_utilization_bar()
    {
        var baseDir = @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec";
        var specPath = Path.Combine(baseDir, "lus-dev-stakeholder.dashspec");
        if (!File.Exists(specPath))
        {
            return;
        }

        var doc = DashSpecParser.Parse(File.ReadAllText(specPath), baseDir);
        var library = SpecLibraryComposer.Load(specPath, doc.DiagramLibraryPath, doc.PalettePath, baseDir, doc);
        var card = doc.Cards.Single(c => c.Id == "stakeholder_utilization");
        var resolved = CardDiagramResolver.Resolve(card, library);
        var presentation = CardChromeResolver.ResolveChartPresentation(resolved.Card, library);

        Assert.Equal(ChartAxisScale.Percent, presentation.ValueAxisScale);
        Assert.Equal(100, presentation.ValueAxisMax);
    }

    [Fact]
    public void CategoryChartPayloadBuilder_marks_bars_over_percent_cap_red()
    {
        var library = SpecLibrary.Parse(
        [
            "[presentation.bar_utilization_percent]",
            "color_mode = \"single\"",
            "default = \"#60a5fa\"",
            "y_max = \"100\"",
        ]);

        var card = DashSpecParser.Parse("""
            @dashboard t
              configuration
              diagramlibrary = "lib.toml"
              end configuration
              report
              title = "T"
              card c as "C"
              diagram bar
              category = app_name
              value = utilization_pct
              scale_value = percent
              end bar
              presentation
              use = bar_utilization_percent
              end presentation
              datasource view dbo.t
              end card
              end report
            end dashboard
""").Cards[0];

        var resolved = CardDiagramResolver.Resolve(card, library).Card;
        var payload = ChartDataBuilder.BuildLineOrBar(
            [
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["app_name"] = "AutoCAD",
                    ["utilization_pct"] = 104d,
                },
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["app_name"] = "Revit",
                    ["utilization_pct"] = 80d,
                },
            ],
            resolved.Diagram,
            seriesTransform: null,
            resolved,
            library);

        Assert.Equal("#ef4444", payload.Series[0].PointColors![0]);
        Assert.Equal("#60a5fa", payload.Series[0].PointColors![1]);
    }
}
