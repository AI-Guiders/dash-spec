using DashSpec.Core.Analysis;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;

namespace DashSpec.Core.Layout;

/// <summary>Resolved tab cards and layout board for placement.</summary>
public sealed record TabLayoutContext(
    TabDefinition Tab,
    IReadOnlyList<CardDefinition> TabCards,
    LayoutBoardDefinition? Board,
    int Columns);

/// <summary>
/// Per-tab grid placement: bracket board, explicit place, or auto compaction.
/// </summary>
public static class TabLayoutCompactor
{
    public static TabLayoutContext ResolveContext(
        DashboardDocument document,
        string tabId,
        string? activePageId = null)
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

        var effectivePageId = activePageId;
        var tabPages = PageTabScope.FilterForTab(document.Pages, tabId);
        if (string.IsNullOrWhiteSpace(effectivePageId) && tabPages.Count > 0)
        {
            effectivePageId = tabPages[0].Id;
        }

        if (!string.IsNullOrWhiteSpace(effectivePageId))
        {
            tabCards = tabCards
                .Where(card => string.Equals(card.PageId, effectivePageId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var board = tab.LayoutBoard;
        if (!string.IsNullOrWhiteSpace(effectivePageId))
        {
            var page = tabPages.FirstOrDefault(p =>
                string.Equals(p.Id, effectivePageId, StringComparison.OrdinalIgnoreCase));
            if (page?.LayoutBoard is not null)
            {
                board = page.LayoutBoard;
            }
        }

        return new TabLayoutContext(tab, tabCards, board, document.Layout.Columns);
    }

    public static TabLayoutPlan? TryBuildLayoutPlan(TabLayoutContext context) =>
        context.Board is null
            ? null
            : TabLayoutPlanner.Plan(context.Board, context.TabCards, context.Columns, context.Tab.Id);

    public static string ResolveCardRef(TabLayoutContext context, string token) =>
        CardLayoutRefResolver.Resolve(token, context.TabCards, $"Tab '{context.Tab.Id}' layout");

    public static IReadOnlyDictionary<string, PlacementDefinition> Compact(
        DashboardDocument document,
        string tabId,
        SpecLibrary? library = null,
        string? activePageId = null)
    {
        var context = ResolveContext(document, tabId, activePageId);
        var tab = context.Tab;
        var tabCards = context.TabCards;
        var columns = context.Columns;
        Dictionary<string, PlacementDefinition> result;
        var board = context.Board;

        if (board is not null)
        {
            result = new Dictionary<string, PlacementDefinition>(
                TabLayoutBoardResolver.Resolve(board, tabCards, columns, tab.Id),
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
