using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

/// <summary>Card grid placement precedence (ADR-0057 §7, ADR-0020).</summary>
public static class LayoutPlacementResolution
{
    /// <summary>Explicit <c>place</c> on card overrides layout board placement.</summary>
    public static PlacementDefinition ResolveCardPlacement(
        CardDefinition card,
        IReadOnlyDictionary<string, PlacementDefinition> boardPlacements)
    {
        if (card.Placement is not null)
        {
            return card.Placement;
        }

        if (boardPlacements.TryGetValue(card.Id, out var placement))
        {
            return placement;
        }

        throw new KeyNotFoundException(
            $"Card '{card.Id}' has no placement on the layout board and no explicit place block.");
    }

    public static void ApplyExplicitPlacementOverrides(
        IEnumerable<CardDefinition> cards,
        IDictionary<string, PlacementDefinition> boardPlacements)
    {
        foreach (var card in cards)
        {
            if (card.Placement is not null)
            {
                boardPlacements[card.Id] = card.Placement;
            }
        }
    }
}
