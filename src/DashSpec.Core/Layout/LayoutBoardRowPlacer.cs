using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

/// <summary>Maps one bracket board row to grid placements (ADR-0020, ADR-0061 weights).</summary>
public static class LayoutBoardRowPlacer
{
    public static void PlaceRow(
        IReadOnlyList<string> row,
        int gridRow,
        int columns,
        string context,
        Func<string, string> resolveToken,
        IDictionary<string, PlacementDefinition> result)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(resolveToken);
        ArgumentNullException.ThrowIfNull(result);
        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        if (row.Count == 0)
        {
            throw new DashSpecParseException($"{context}: row {gridRow} is empty.");
        }

        if (row.Count == 1)
        {
            var cell = ParseCell(row[0], context, gridRow);
            var itemId = resolveToken(cell.RefToken);
            if (!result.TryAdd(itemId, new PlacementDefinition(gridRow, 1, columns)))
            {
                throw new DashSpecParseException(
                    $"{context}: '{itemId}' appears more than once in the layout board.");
            }

            return;
        }

        var cells = row.Select(token => ParseCell(token, context, gridRow)).ToList();
        var totalWeight = cells.Sum(cell => cell.Weight);
        if (totalWeight <= 0)
        {
            throw new DashSpecParseException($"{context}: row {gridRow} has invalid weights.");
        }

        var col = 1;
        var allocated = 0;
        for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
        {
            var span = cellIndex == cells.Count - 1
                ? columns - allocated
                : columns * cells[cellIndex].Weight / totalWeight;
            if (span <= 0)
            {
                throw new DashSpecParseException(
                    $"{context}: row {gridRow} weight distribution yields zero-width cell.");
            }

            var itemId = resolveToken(cells[cellIndex].RefToken);
            if (!result.TryAdd(itemId, new PlacementDefinition(gridRow, col, span)))
            {
                throw new DashSpecParseException(
                    $"{context}: '{itemId}' appears more than once in the layout board.");
            }

            col += span;
            allocated += span;
        }
    }

    internal static BoardCell ParseCell(string token, string context, int gridRow)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new DashSpecParseException($"{context}: row {gridRow} has an empty cell.");
        }

        var separator = token.IndexOf(':');
        if (separator < 0)
        {
            return new BoardCell(token, 1);
        }

        if (separator == 0 || separator == token.Length - 1)
        {
            throw new DashSpecParseException(
                $"{context}: row {gridRow} cell '{token}' must use form ref:weight.");
        }

        var refToken = token[..separator];
        var weightText = token[(separator + 1)..];
        if (!int.TryParse(weightText, out var weight) || weight <= 0)
        {
            throw new DashSpecParseException(
                $"{context}: row {gridRow} cell '{token}' requires a positive integer weight.");
        }

        return new BoardCell(refToken, weight);
    }

    internal readonly record struct BoardCell(string RefToken, int Weight);
}
