using DashSpec.Core.Model;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Host.Tests;

public sealed class DashboardMatrixCommandTargetsTests
{
    [Fact]
    public void BuildMatrix_includes_only_heatmap_cards()
    {
        var cards = new List<CardDefinition>
        {
            new(
                "line_card",
                "Line",
                new DiagramDefinition("line", new Dictionary<string, string>()),
                new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
                BoundFilters: [],
                LocalFilters: []),
            new(
                "heat_card",
                "Heat",
                new DiagramDefinition("heatmap", new Dictionary<string, string>()),
                new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
                BoundFilters: [],
                LocalFilters: []),
        };

        var targets = DashSpec.Host.Commands.DashboardCardCommandTargetsBuilder.BuildMatrix(cards);

        Assert.Single(targets);
        Assert.Equal("heat_card", targets[0].CardId);
        Assert.Equal("Heat", targets[0].Title);
    }
}
