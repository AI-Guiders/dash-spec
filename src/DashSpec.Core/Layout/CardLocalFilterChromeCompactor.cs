using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

/// <summary>Weighted chrome row for card-local filters (ADR-0061, filters block layout).</summary>
public static class CardLocalFilterChromeCompactor
{
    public const string ApplySlotId = "apply";

    public static IReadOnlyDictionary<string, PlacementDefinition> Compact(
        CardDefinition card,
        IReadOnlyList<FilterDefinition> filters,
        int columns)
    {
        if (card.LocalFilters is not { Count: > 0 } || card.LocalFiltersChromeBoard is null)
        {
            return new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        var row = card.LocalFiltersChromeBoard.Rows.FirstOrDefault();
        if (row is null or { Count: 0 })
        {
            return new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        var context = $"Card '{card.Id}' filters layout";
        var chromeColumns = ResolveChromeGridColumns(row, context);
        var result = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        LayoutBoardRowPlacer.PlaceRow(
            row,
            1,
            chromeColumns,
            context,
            token => ResolveChromeToken(token, card, filters),
            result);
        LayoutPlacementResolution.ApplyFilterPlacementOverrides(
            filters.Where(f => card.LocalFilters.Contains(f.Name, StringComparer.OrdinalIgnoreCase)),
            result);
        return result;
    }

    private static string ResolveChromeToken(
        string token,
        CardDefinition card,
        IReadOnlyList<FilterDefinition> filterDefs)
    {
        if (string.Equals(token, ApplySlotId, StringComparison.OrdinalIgnoreCase))
        {
            return ApplySlotId;
        }

        return FilterLayoutRefResolver.Resolve(token, filterDefs, $"Card '{card.Id}' filters layout");
    }

    /// <summary>Chrome row weights define a local grid (e.g. 2+1+1 → 4 cols), not the page layout width.</summary>
    internal static int ResolveChromeGridColumns(IReadOnlyList<string> row, string context)
    {
        if (row.Count == 0)
        {
            throw new DashSpecParseException($"{context}: row is empty.");
        }

        if (row.Count == 1)
        {
            return 1;
        }

        return row
            .Select(token => LayoutBoardRowPlacer.ParseCell(token, context, 1))
            .Sum(cell => cell.Weight);
    }
}
