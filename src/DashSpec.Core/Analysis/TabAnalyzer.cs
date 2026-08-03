using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Analysis;

internal static class TabAnalyzer
{
    public static void Validate(DashboardDocument document)
    {
        if (document.Tabs.Count == 0)
        {
            return;
        }

        var cardsById = document.Cards.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);
        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tab in document.Tabs)
        {
            var tabCards = new List<CardDefinition>();

            foreach (var token in tab.CardIds)
            {
                var cardId = CardLayoutRefResolver.Resolve(
                    token,
                    document.Cards,
                    $"Tab '{tab.Id}' cards");

                if (!cardsById.ContainsKey(cardId))
                {
                    throw new DashSpecParseException(
                        $"Tab '{tab.Id}' references unknown card '{cardId}'.");
                }

                if (!assigned.Add(cardId))
                {
                    throw new DashSpecParseException(
                        $"Card '{cardId}' is listed in more than one tab.");
                }

                tabCards.Add(cardsById[cardId]);
            }

            ValidateLayoutRefs(tab, tabCards);

            var tabPages = PageTabScope.FilterForTab(document.Pages, tab.Id);
            if (PageTabScope.TabDeclaresPages(document.Pages, tab.Id, document.Tabs.Count))
            {
                if (tabPages.Any(page => string.Equals(page.TabId, tab.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    tabPages = tabPages
                        .Where(page => string.Equals(page.TabId, tab.Id, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }

                foreach (var page in tabPages)
                {
                    var pageCards = tabCards
                        .Where(card => string.Equals(card.PageId, page.Id, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (pageCards.Count == 0 || page.LayoutBoard is null)
                    {
                        continue;
                    }

                    ValidateLayoutBoard(tab, pageCards, page.LayoutBoard, document.Layout.Columns);
                }
            }
            else if (tab.LayoutBoard is not null)
            {
                ValidateLayoutBoard(tab, tabCards, tab.LayoutBoard, document.Layout.Columns);
            }
        }

        foreach (var card in document.Cards)
        {
            if (string.IsNullOrWhiteSpace(card.TabId))
            {
                throw new DashSpecParseException(
                    $"Card '{card.Id}' is not assigned to any tab.");
            }
        }
    }

    private static void ValidateLayoutRefs(TabDefinition tab, IReadOnlyList<CardDefinition> tabCards)
    {
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var card in tabCards)
        {
            if (string.IsNullOrWhiteSpace(card.LayoutRef))
            {
                continue;
            }

            if (!refs.Add(card.LayoutRef))
            {
                throw new DashSpecParseException(
                    $"Tab '{tab.Id}': duplicate card ref '{card.LayoutRef}'.");
            }
        }
    }

    private static void ValidateLayoutBoard(
        TabDefinition tab,
        IReadOnlyList<CardDefinition> tabCards,
        LayoutBoardDefinition board,
        int columns)
    {
        var placed = TabLayoutBoardResolver.Resolve(board, tabCards, columns, tab.Id);

        if (placed.Count != tabCards.Count)
        {
            var missing = tabCards
                .Select(c => c.Id)
                .Where(id => !placed.ContainsKey(id))
                .ToList();
            throw new DashSpecParseException(
                $"Tab '{tab.Id}' layout board omits cards: {string.Join(", ", missing)}.");
        }
    }
}
