using DashSpec.Abstractions.Data;

namespace DashSpec.Execution.Runtime;

/// <summary>Compile-time layout contracts: board tokens resolve against the cards
/// they name (TabLayoutPlanner), card interiors cover their slots (the interior
/// compactor). Everything here is spec-visible — a typo in a layout token is a
/// compile defect, not a first render surprise. Runs with the document's own
/// grid column count.</summary>
internal static class LayoutAnalyzer
{
    public static void Validate(DashboardDocument document)
    {
        var columns = document.Layout.Columns;
        var cardsById = new Dictionary<string, CardDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in document.Cards)
        {
            cardsById.TryAdd(card.Id, card);
        }

        foreach (var tab in document.Tabs)
        {
            var tabCards = tab.CardIds
                .Select(id => cardsById.TryGetValue(id, out var card) ? card : null)
                .Where(card => card is not null)
                .Select(card => card!)
                .ToList();

            if (tab.LayoutBoard?.Value is { } board)
            {
                // Throws on unknown card tokens, undeclared nests, bad rows —
                // the same failures the first render would hit.
                TabLayoutPlanner.Plan(board, tabCards, columns, tab.Id);
            }

            foreach (var page in document.Pages?.Value ?? [])
            {
                if (page.LayoutBoard?.Value is { } pageBoard
                    && string.Equals(page.TabId?.Value, tab.Id, StringComparison.OrdinalIgnoreCase))
                {
                    TabLayoutPlanner.Plan(pageBoard, tabCards, columns, $"{tab.Id}/{page.Id}");
                }
            }
        }

        foreach (var card in document.Cards)
        {
            CardInteriorLayoutCompactor.Compact(card, document.Filters, columns);
        }
    }
}
