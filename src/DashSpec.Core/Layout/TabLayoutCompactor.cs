using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;

namespace DashSpec.Core.Layout;

/// <summary>
/// Per-tab grid placement: bumps cards to the next row when column spans overlap.
/// </summary>
public static class TabLayoutCompactor
{
    public static IReadOnlyDictionary<string, PlacementDefinition> Compact(
        DashboardDocument document,
        string tabId,
        SpecLibrary? library = null)
    {
        var tab = document.Tabs.Single(t =>
            string.Equals(t.Id, tabId, StringComparison.OrdinalIgnoreCase));

        var columns = document.Layout.Columns;
        var result = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        var occupied = new List<RowSlot>();

        foreach (var cardId in tab.CardIds)
        {
            var card = document.Cards.Single(c =>
                string.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase));

            var kind = DiagramKindRegistry.Resolve(CardResolver.ResolveKind(card, library, document.DashboardFilters));
            var placement = card.Placement ?? DefaultPlacement(kind.DataFamily, columns);
            var row = placement.Row > 0 ? placement.Row : 1;
            var col = placement.Col > 0 ? placement.Col : 1;
            var span = Math.Min(placement.Span, columns);

            while (Overlaps(row, col, span, occupied))
            {
                row++;
            }

            result[cardId] = new PlacementDefinition(row, col, span);
            occupied.Add(new RowSlot(row, col, col + span - 1));
        }

        return result;
    }

    private static bool Overlaps(int row, int col, int span, IReadOnlyList<RowSlot> occupied)
    {
        var end = col + span - 1;
        foreach (var slot in occupied)
        {
            if (slot.Row != row)
            {
                continue;
            }

            if (col <= slot.ColEnd && end >= slot.ColStart)
            {
                return true;
            }
        }

        return false;
    }

    private static PlacementDefinition DefaultPlacement(DiagramDataFamily family, int columns) =>
        family is DiagramDataFamily.Table or DiagramDataFamily.Matrix
            ? new PlacementDefinition(Row: 1, Col: 1, Span: columns)
            : new PlacementDefinition(Span: columns / 2);

    private readonly record struct RowSlot(int Row, int ColStart, int ColEnd);
}
