using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
using DashSpec.Host.Services.Presentation;
using DashSpec.Viz;
using Xunit;

namespace DashSpec.Host.Tests;

public sealed class CardVizDisplayToggleTests
{
    [Fact]
    public void TryApply_legacy_matrix_action_toggles_axis_y()
    {
        var state = new CardVizDisplayStateService();
        var card = HeatmapCard("h1");
        var request = new CardActionRequest(
            "h1",
            VizCardDisplayActions.LegacyToggleAxisLabelsY,
            new Dictionary<string, string>());

        Assert.True(CardVizDisplayToggle.TryApply(request, card, state));
        Assert.False(state.GetAxisLabelsYOverride("h1"));
    }

    private static CardRenderResult HeatmapCard(string id)
    {
        var definition = new CardDefinition(
            id,
            "Heat",
            new DiagramDefinition(
                "heatmap",
                new Dictionary<string, string> { ["axis_labels_y"] = "show" }),
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            BoundFilters: [],
            LocalFilters: []);

        var presentation = MatrixPresentation.FromCard(definition);
        var matrix = new MatrixPayload(["x"], ["y"], [[1.0]], 0, 1);
        return new CardRenderResult(
            definition.Id,
            definition.Title,
            definition.Diagram.Kind,
            DiagramDataFamily.Matrix,
            "matrix-canvas",
            Matrix: matrix,
            MatrixPresentation: presentation,
            ExtensionBlocks: []);
    }
}
