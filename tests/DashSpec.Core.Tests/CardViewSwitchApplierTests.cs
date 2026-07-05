using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class CardViewSwitchApplierTests
{
    [Fact]
    public void Apply_swaps_diagram_preset_for_active_view()
    {
        var card = new CardDefinition(
            "peak",
            "Peak",
            new DiagramDefinition(string.Empty, new Dictionary<string, string>(), "default_diagram"),
            new DataSourceDefinition(DataSourceKind.View, "dbo.v"),
            [],
            [],
            ExtensionBlocks:
            [
                new ExtensionBlockNode(
                    "views",
                    new Dictionary<string, string> { ["default"] = "heatmap" },
                    [
                        new ExtensionBlockNode("line", new Dictionary<string, string> { ["diagram"] = "line_diagram" }, []),
                        new ExtensionBlockNode("heatmap", new Dictionary<string, string> { ["diagram"] = "heatmap_diagram" }, []),
                    ]),
            ]);

        var applied = CardViewSwitchApplier.Apply(card, "line");

        Assert.Equal("line_diagram", applied.Diagram.UsePreset);
    }
}
