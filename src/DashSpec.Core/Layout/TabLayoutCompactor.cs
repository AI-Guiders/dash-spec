using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;

namespace DashSpec.Core.Layout;

/// <summary>
/// Per-tab grid placement: bracket board, explicit place, or auto compaction.
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

        var tabCards = tab.CardIds
            .Select(token =>
            {
                var cardId = CardLayoutRefResolver.Resolve(
                    token,
                    document.Cards,
                    $"Tab '{tab.Id}' cards");
                return document.Cards.Single(c =>
                    string.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase));
            })
            .ToList();

        var columns = document.Layout.Columns;
        Dictionary<string, PlacementDefinition> result;

        if (tab.LayoutBoard is not null)
        {
            result = new Dictionary<string, PlacementDefinition>(
                TabLayoutBoardResolver.Resolve(tab.LayoutBoard, tabCards, columns, tab.Id),
                StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            result = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
            var occupied = new List<RowSlot>();

            foreach (var card in tabCards)
            {
                var kind = DiagramKindRegistry.Resolve(
                    CardResolver.ResolveKind(card, library, document.DashboardFilters));
                var placement = card.Placement ?? PlacementDefaults.ForFamily(kind.DataFamily, columns);
                var row = placement.Row > 0 ? placement.Row : 1;
                var col = placement.Col > 0 ? placement.Col : 1;
                var span = Math.Min(placement.Span, columns);

                while (Overlaps(row, col, span, occupied))
                {
                    row++;
                }

                result[card.Id] = new PlacementDefinition(row, col, span);
                occupied.Add(new RowSlot(row, col, col + span - 1));
            }

            return result;
        }

        foreach (var card in tabCards)
        {
            if (card.Placement is not null)
            {
                result[card.Id] = card.Placement;
            }
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

    private readonly record struct RowSlot(int Row, int ColStart, int ColEnd);
}
