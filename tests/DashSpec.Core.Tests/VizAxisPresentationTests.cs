using DashSpec.Core.Model;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class VizAxisPresentationTests
{
    [Theory]
    [InlineData("show", VizValueLabelMode.Show)]
    [InlineData("hide", VizValueLabelMode.Hide)]
    [InlineData("auto", VizValueLabelMode.Auto)]
    public void ParseValueLabels_reads_spec_property(string raw, VizValueLabelMode expected)
    {
        var props = new Dictionary<string, string> { ["value_labels"] = raw };
        Assert.Equal(expected, VizAxisPresentationParser.ParseValueLabels(props));
    }

    [Theory]
    [InlineData("show", true)]
    [InlineData("hide", false)]
    public void ParseAxisLabels_reads_spec_property(string raw, bool expected)
    {
        var props = new Dictionary<string, string> { ["axis_labels_x"] = raw };
        Assert.Equal(expected, VizAxisPresentationParser.ParseAxisLabels(props, "axis_labels_x"));
    }

    [Theory]
    [InlineData("10", 10)]
    [InlineData("14", 14)]
    [InlineData("3", 6)]
    [InlineData("120", 96)]
    [InlineData("nope", VizAxisPresentationParser.DefaultValueLabelsThresholdPx)]
    public void ParseValueLabelsThreshold_clamps_and_defaults(string raw, int expected)
    {
        var props = new Dictionary<string, string> { ["value_labels_threshold"] = raw };
        Assert.Equal(expected, VizAxisPresentationParser.ParseValueLabelsThreshold(props));
    }

    [Fact]
    public void MatrixPresentation_reads_label_visibility_from_diagram()
    {
        var card = new CardDefinition(
            "t",
            "T",
            new DiagramDefinition(
                "heatmap",
                new Dictionary<string, string>
                {
                    ["value_labels"] = "hide",
                    ["axis_labels_x"] = "hide",
                    ["axis_labels_y"] = "show",
                }),
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            BoundFilters: [],
            LocalFilters: []);

        var presentation = MatrixPresentation.FromCard(card);
        Assert.Equal(VizValueLabelMode.Hide, presentation.ValueLabels);
        Assert.Equal(VizAxisPresentationParser.DefaultValueLabelsThresholdPx, presentation.ValueLabelsThresholdPx);
        Assert.False(presentation.AxisLabelsX);
        Assert.True(presentation.AxisLabelsY);
    }

    [Fact]
    public void MatrixPresentation_reads_toolbar_labels_from_diagram()
    {
        var card = new CardDefinition(
            "t",
            "T",
            new DiagramDefinition(
                "heatmap",
                new Dictionary<string, string>
                {
                    ["toolbar_value_labels"] = "Значения",
                    ["toolbar_axis_labels_x"] = "Дни",
                    ["toolbar_axis_labels_y"] = "Пользователи",
                }),
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            BoundFilters: [],
            LocalFilters: []);

        var presentation = MatrixPresentation.FromCard(card);
        Assert.Equal("Значения", presentation.ToolbarValueLabels);
        Assert.Equal("Дни", presentation.ToolbarAxisLabelsX);
        Assert.Equal("Пользователи", presentation.ToolbarAxisLabelsY);
    }

    [Fact]
    public void MatrixPresentation_reads_value_labels_threshold_from_diagram()
    {
        var card = new CardDefinition(
            "t",
            "T",
            new DiagramDefinition(
                "heatmap",
                new Dictionary<string, string> { ["value_labels_threshold"] = "18" }),
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            BoundFilters: [],
            LocalFilters: []);

        var presentation = MatrixPresentation.FromCard(card);
        Assert.Equal(18, presentation.ValueLabelsThresholdPx);
    }

    [Theory]
    [InlineData(VizValueLabelMode.Auto, null, "auto")]
    [InlineData(VizValueLabelMode.Show, null, "show")]
    [InlineData(VizValueLabelMode.Hide, null, "hide")]
    [InlineData(VizValueLabelMode.Auto, true, "show")]
    [InlineData(VizValueLabelMode.Hide, true, "show")]
    public void ResolveValueLabelsWire_prefers_user_override(
        VizValueLabelMode specMode,
        bool? userOverride,
        string expectedWire)
    {
        Assert.Equal(
            expectedWire,
            VizLabelDisplayResolver.ResolveValueLabelsWire(specMode, userOverride));
    }
}
