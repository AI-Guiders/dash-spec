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
        PlaceRow(
            row,
            gridRow,
            columns,
            context,
            token => new LayoutBoardRefTarget(LayoutBoardRefKind.Card, resolveToken(token)),
            (target, placement, cards, _, _) =>
            {
                if (target.Kind != LayoutBoardRefKind.Card)
                {
                    throw new DashSpecParseException(
                        $"{context}: row {gridRow} references nest '{target.Id}' but nest cells require TabLayoutPlanner.");
                }

                if (!cards.TryAdd(target.Id, placement))
                {
                    throw new DashSpecParseException(
                        $"{context}: '{target.Id}' appears more than once in the layout board.");
                }
            },
            result,
            EmptyNestPlacements,
            EmptyNestDefinitions);
    }

    private static readonly Dictionary<string, LayoutNestPlacement> EmptyNestPlacements = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, LayoutBoardNestDefinition> EmptyNestDefinitions = new(StringComparer.OrdinalIgnoreCase);

    public static void PlaceRow(
        IReadOnlyList<string> row,
        int gridRow,
        int columns,
        string context,
        Func<string, LayoutBoardRefTarget> resolveTarget,
        Action<LayoutBoardRefTarget, PlacementDefinition, IDictionary<string, PlacementDefinition>, IDictionary<string, LayoutNestPlacement>, IReadOnlyDictionary<string, LayoutBoardNestDefinition>> assignCell,
        IDictionary<string, PlacementDefinition> cardPlacements,
        IDictionary<string, LayoutNestPlacement> nestPlacements,
        IReadOnlyDictionary<string, LayoutBoardNestDefinition> nestDefinitions)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(resolveTarget);
        ArgumentNullException.ThrowIfNull(assignCell);
        ArgumentNullException.ThrowIfNull(cardPlacements);
        ArgumentNullException.ThrowIfNull(nestPlacements);
        ArgumentNullException.ThrowIfNull(nestDefinitions);
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
            var target = resolveTarget(cell.RefToken);
            var placement = new PlacementDefinition(gridRow, 1, columns);
            assignCell(target, placement, cardPlacements, nestPlacements, nestDefinitions);
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

            var target = resolveTarget(cells[cellIndex].RefToken);
            var placement = new PlacementDefinition(gridRow, col, span);
            assignCell(target, placement, cardPlacements, nestPlacements, nestDefinitions);
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
