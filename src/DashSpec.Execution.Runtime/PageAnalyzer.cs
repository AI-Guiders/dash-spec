using DashSpec.Core.Parsing;

namespace DashSpec.Execution.Runtime;

internal static class PageAnalyzer
{
    public static void Validate(DashboardDocument document)
    {
        var pages = document.Pages?.Value ?? [];
        if (pages.Count == 0)
        {
            return;
        }

        var duplicatePage = pages
            .GroupBy(
                page => (TabKey: page.TabId?.Value?.ToLowerInvariant() ?? "", PageKey: page.Id.ToLowerInvariant()))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicatePage is not null)
        {
            var sample = duplicatePage.First();
            throw new DashSpecParseException(
                $"Report declares duplicate page id '{sample.Id}'" +
                (string.IsNullOrWhiteSpace(sample.TabId?.Value) ? "." : $" on tab '{sample.TabId?.Value}'."));
        }

        if (document.Tabs.Count == 0)
        {
            ValidateTabCardsInPages(document.Cards, pages, "report");
            ValidatePageLayouts(document.Cards, pages, document.Layout.Columns);
            return;
        }

        foreach (var tab in document.Tabs)
        {
            if (!PageTabScope.TabDeclaresPages(pages, tab.Id, document.Tabs.Count))
            {
                continue;
            }

            var tabPages = PageTabScope.FilterForTab(pages, tab.Id);
            if (tabPages.Any(page => string.Equals(page.TabId?.Value, tab.Id, StringComparison.OrdinalIgnoreCase)))
            {
                tabPages = tabPages
                    .Where(page => string.Equals(page.TabId?.Value, tab.Id, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var tabCards = document.Cards
                .Where(card => string.Equals(card.TabId?.Value, tab.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();

            ValidateTabCardsInPages(tabCards, tabPages, tab.Id);
            ValidatePageLayouts(tabCards, tabPages, document.Layout.Columns);
        }
    }

    private static void ValidateTabCardsInPages(
        IReadOnlyList<CardDefinition> tabCards,
        IReadOnlyList<ReportPageDefinition> tabPages,
        string tabId)
    {
        var pageIdSet = tabPages
            .Select(page => page.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var card in tabCards)
        {
            var pageId = card.PageId?.Value;
            if (string.IsNullOrWhiteSpace(pageId))
            {
                throw new DashSpecParseException(
                    $"Card '{card.Id}' must be inside a page block when tab '{tabId}' declares pages.");
            }

            if (!pageIdSet.Contains(pageId))
            {
                throw new DashSpecParseException(
                    $"Card '{card.Id}' references unknown page '{pageId}' on tab '{tabId}'.");
            }
        }
    }

    private static void ValidatePageLayouts(
        IReadOnlyList<CardDefinition> tabCards,
        IReadOnlyList<ReportPageDefinition> tabPages,
        int columns)
    {
        foreach (var page in tabPages)
        {
            var pageCards = tabCards
                .Where(card => string.Equals(card.PageId?.Value, page.Id, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (pageCards.Count == 0 || page.LayoutBoard is null)
            {
                continue;
            }

            var placed = TabLayoutBoardResolver.Resolve(page.LayoutBoard!.Value, pageCards, columns, page.Id);
            if (placed.Count != pageCards.Count)
            {
                var missing = pageCards
                    .Select(card => card.Id)
                    .Where(id => !placed.ContainsKey(id))
                    .ToList();
                throw new DashSpecParseException(
                    $"Page '{page.Id}' layout board omits cards: {string.Join(", ", missing)}.");
            }
        }
    }
}
