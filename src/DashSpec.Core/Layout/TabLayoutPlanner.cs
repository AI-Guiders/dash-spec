using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

/// <summary>Resolves tab layout boards with optional card groups into a render plan.</summary>
public static class TabLayoutPlanner
{
    public static TabLayoutPlan Plan(
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
        var topLevel = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        var allCards = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        var groups = new Dictionary<string, LayoutGroupPlacement>(StringComparer.OrdinalIgnoreCase);
        var outerRow = 0;

        foreach (var entry in board.Entries)
        {
            outerRow++;
            switch (entry)
            {
                case LayoutBoardCardRow cardRow:
                    PlaceCardRow(
                        cardRow.CardIds,
                        outerRow,
                        columns,
                        context,
                        token => CardLayoutRefResolver.Resolve(token, tabCards, context),
                        topLevel,
                        allCards);
                    break;
                case LayoutBoardGroupRow { Group: var group }:
                    if (groups.ContainsKey(group.Id))
                    {
                        throw new DashSpecParseException(
                            $"{context}: duplicate layout group id '{group.Id}'.");
                    }

                    var innerBoard = LayoutBoardDefinition.FromCardRows(group.Rows);
                    var inner = LayoutBoardPlacer.Resolve(
                        innerBoard,
                        columns,
                        $"{context} group '{group.Id}'",
                        token => CardLayoutRefResolver.Resolve(token, tabCards, context));

                    groups[group.Id] = new LayoutGroupPlacement(group.Id, group.Title, outerRow, inner);
                    foreach (var (cardId, placement) in inner)
                    {
                        allCards[cardId] = placement;
                    }

                    break;
            }
        }

        return new TabLayoutPlan(board.Entries, topLevel, groups, allCards);
    }

    private static void PlaceCardRow(
        IReadOnlyList<string> row,
        int gridRow,
        int columns,
        string context,
        Func<string, string> resolveToken,
        IDictionary<string, PlacementDefinition> topLevel,
        IDictionary<string, PlacementDefinition> allCards)
    {
        if (row.Count == 0)
        {
            throw new DashSpecParseException($"{context}: row {gridRow} is empty.");
        }

        var cellCount = row.Count;
        var span = cellCount == 1 ? columns : columns / cellCount;

        for (var cellIndex = 0; cellIndex < cellCount; cellIndex++)
        {
            var itemId = resolveToken(row[cellIndex]);
            var placement = new PlacementDefinition(gridRow, 1 + cellIndex * span, span);
            if (!topLevel.TryAdd(itemId, placement))
            {
                throw new DashSpecParseException(
                    $"{context}: '{itemId}' appears more than once in the layout board.");
            }

            allCards[itemId] = placement;
        }
    }
}
