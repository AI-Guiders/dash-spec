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

        var cardIds = document.Cards
            .Select(c => c.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tab in document.Tabs)
        {
            foreach (var cardId in tab.CardIds)
            {
                if (!cardIds.Contains(cardId))
                {
                    throw new DashSpecParseException(
                        $"Tab '{tab.Id}' references unknown card '{cardId}'.");
                }

                if (!assigned.Add(cardId))
                {
                    throw new DashSpecParseException(
                        $"Card '{cardId}' is listed in more than one tab.");
                }
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
}
