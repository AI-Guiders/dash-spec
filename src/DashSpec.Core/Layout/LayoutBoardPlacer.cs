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

        for (var rowIndex = 0; rowIndex < board.Rows.Count; rowIndex++)
        {
            var row = board.Rows[rowIndex];
            if (row.Count == 0)
            {
                throw new DashSpecParseException($"{context}: row {rowIndex + 1} is empty.");
            }

            var gridRow = rowIndex + 1;
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

        return result;
    }
}
