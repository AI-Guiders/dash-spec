using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

/// <summary>Maps tab layout board rows to grid placements on the dashboard column count.</summary>
public static class TabLayoutBoardResolver
{
    public static IReadOnlyDictionary<string, PlacementDefinition> Resolve(
        LayoutBoardDefinition board,
        IReadOnlyList<CardDefinition> tabCards,
        int columns,
        string tabId)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(tabCards);
        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        var context = $"Tab '{tabId}' layout";
        return LayoutBoardPlacer.Resolve(
            board,
            columns,
            context,
            token => CardLayoutRefResolver.Resolve(token, tabCards, context));
    }
}
