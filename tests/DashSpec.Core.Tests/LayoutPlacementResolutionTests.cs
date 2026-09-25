using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class LayoutPlacementResolutionTests
{
    private static readonly CardDefinition Card = new(
        "card_a",
        "",
        new DiagramDefinition("bar", new Dictionary<string, string>()),
        new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
        [],
        [],
        Placement: new PlacementDefinition(2, 1, 6));

    [Fact]
    public void ResolveCardPlacement_prefers_explicit_place()
    {
        var board = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [Card.Id] = new PlacementDefinition(1, 1, 6),
        };

        var placement = LayoutPlacementResolution.ResolveCardPlacement(Card, board);
        Assert.Equal(2, placement.Row);
    }

    [Fact]
    public void ApplyExplicitPlacementOverrides_overwrites_board()
    {
        var board = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [Card.Id] = new PlacementDefinition(1, 1, 6),
        };

        LayoutPlacementResolution.ApplyExplicitPlacementOverrides([Card], board);
        Assert.Equal(2, board[Card.Id].Row);
    }
}
