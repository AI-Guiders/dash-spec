using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

/// <summary>Maps bracket board rows to grid placements on a column count.</summary>
internal static class LayoutBoardPlacer
{
    public static IReadOnlyDictionary<string, PlacementDefinition> Resolve(
        LayoutBoardDefinition board,
        int columns,
        string context,
        Func<string, string> resolveToken)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(resolveToken);
        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        var result = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        var rowIndex = 0;

        foreach (var entry in board.Entries)
        {
            switch (entry)
            {
                case LayoutBoardCardRow cardRow:
                    rowIndex++;
                    PlaceCardRow(cardRow.CardIds, rowIndex, columns, context, resolveToken, result);
                    break;
                case LayoutBoardGroupRow { Group: var group }:
                    rowIndex++;
                    var innerBoard = LayoutBoardDefinition.FromCardRows(group.Rows);
                    var inner = Resolve(
                        innerBoard,
                        columns,
                        $"{context} group '{group.Id}'",
                        resolveToken);
                    foreach (var (cardId, placement) in inner)
                    {
                        if (!result.TryAdd(cardId, placement))
                        {
                            throw new DashSpecParseException(
                                $"{context}: '{cardId}' appears more than once in the layout board.");
                        }
                    }

                    break;
            }
        }

        return result;
    }

    private static void PlaceCardRow(
        IReadOnlyList<string> row,
        int gridRow,
        int columns,
        string context,
        Func<string, string> resolveToken,
        IDictionary<string, PlacementDefinition> result)
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
            if (!result.TryAdd(itemId, new PlacementDefinition(gridRow, 1 + cellIndex * span, span)))
            {
                throw new DashSpecParseException(
                    $"{context}: '{itemId}' appears more than once in the layout board.");
            }
        }
    }
}
