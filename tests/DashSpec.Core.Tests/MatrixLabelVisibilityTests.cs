using DashSpec.Core.Model;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class MatrixLabelVisibilityTests
{
    [Theory]
    [InlineData("show", MatrixValueLabelMode.Show)]
    [InlineData("hide", MatrixValueLabelMode.Hide)]
    [InlineData("auto", MatrixValueLabelMode.Auto)]
    public void ParseValueLabels_reads_spec_property(string raw, MatrixValueLabelMode expected)
    {
        var props = new Dictionary<string, string> { ["value_labels"] = raw };
        Assert.Equal(expected, MatrixLabelVisibilityParser.ParseValueLabels(props));
    }

    [Theory]
    [InlineData("show", true)]
    [InlineData("hide", false)]
    public void ParseAxisLabels_reads_spec_property(string raw, bool expected)
    {
        var props = new Dictionary<string, string> { ["axis_labels_x"] = raw };
        Assert.Equal(expected, MatrixLabelVisibilityParser.ParseAxisLabels(props, "axis_labels_x"));
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
        Assert.Equal(MatrixValueLabelMode.Hide, presentation.ValueLabels);
        Assert.False(presentation.AxisLabelsX);
        Assert.True(presentation.AxisLabelsY);
    }

    [Theory]
    [InlineData(MatrixValueLabelMode.Auto, null, "auto")]
    [InlineData(MatrixValueLabelMode.Show, null, "show")]
    [InlineData(MatrixValueLabelMode.Hide, null, "hide")]
    [InlineData(MatrixValueLabelMode.Auto, true, "show")]
    [InlineData(MatrixValueLabelMode.Hide, true, "show")]
    public void ResolveValueLabelsWire_prefers_user_override(
        MatrixValueLabelMode specMode,
        bool? userOverride,
        string expectedWire)
    {
        Assert.Equal(
            expectedWire,
            MatrixLabelDisplayResolver.ResolveValueLabelsWire(specMode, userOverride));
    }
}
