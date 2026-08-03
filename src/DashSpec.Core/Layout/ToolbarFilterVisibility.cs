using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

/// <summary>Which dashboard filters appear in the toolbar for the active tab/page.</summary>
public static class ToolbarFilterVisibility
{
    public static IReadOnlyList<string> ResolveVisibleFilters(
        DashboardDocument document,
        string? activeTabId,
        string? activePageId,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filtersToCards)
    {
        var activeCardIds = ResolveActiveCardIds(document, activeTabId, activePageId);
        var pageToolbar = PageToolbarResolver.ResolveActiveToolbarBoard(
            document,
            activeTabId,
            activePageId);

        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddCandidate(string? name)
        {
            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
            {
                return;
            }

            candidates.Add(name);
        }

        foreach (var name in document.DashboardFilters)
        {
            AddCandidate(name);
        }

        if (pageToolbar is not null)
        {
            foreach (var name in pageToolbar.Rows.SelectMany(row => row))
            {
                AddCandidate(name);
            }
        }

        if (activeCardIds.Count == 0)
        {
            return candidates;
        }

        return candidates
            .Where(filterName =>
                filtersToCards.TryGetValue(filterName, out var cards) &&
                cards.Any(cardId => activeCardIds.Contains(cardId)))
            .ToList();
    }

    internal static HashSet<string> ResolveActiveCardIds(
        DashboardDocument document,
        string? activeTabId,
        string? activePageId)
    {
        IEnumerable<CardDefinition> cards = document.Cards;

        if (document.Tabs.Count > 0 && !string.IsNullOrWhiteSpace(activeTabId))
        {
            var tab = document.Tabs.FirstOrDefault(t =>
                string.Equals(t.Id, activeTabId, StringComparison.OrdinalIgnoreCase));
            if (tab is null)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var tabCardIds = tab.CardIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            cards = cards.Where(card => tabCardIds.Contains(card.Id));
        }

        if (!string.IsNullOrWhiteSpace(activePageId))
        {
            cards = cards.Where(card =>
                string.Equals(card.PageId, activePageId, StringComparison.OrdinalIgnoreCase));
        }

        return cards.Select(card => card.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
