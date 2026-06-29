using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;

namespace DashSpec.Core.Analysis;

internal static class FilterPlacementAnalyzer
{
    public static void Validate(DashboardDocument document)
    {
        var registry = document.Filters.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var cardLocalOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var filterName in document.DashboardFilters)
        {
            if (!registry.ContainsKey(filterName))
            {
                throw new DashSpecParseException(
                    $"Dashboard filters block references unknown filter '{filterName}'.");
            }

            if (registry[filterName].Kind is FilterKind.Top)
            {
                throw new DashSpecParseException(
                    $"Top filter '{filterName}' cannot be placed in toolbar; use card filters {{ }}.");
            }
        }

        foreach (var card in document.Cards)
        {
            var localSet = new HashSet<string>(card.LocalFilters, StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(card.UseCardPreset))
            {
                var effectiveBind = CardBindResolver.Expand(
                    card.BoundFilters,
                    card.LocalFilters,
                    document.DashboardFilters);

                foreach (var filterName in effectiveBind)
                {
                    var onDashboard = document.DashboardFilters.Contains(filterName, StringComparer.OrdinalIgnoreCase);
                    var onCard = localSet.Contains(filterName);
                    if (!onDashboard && !onCard)
                    {
                        throw new DashSpecParseException(
                            $"Card '{card.Id}': bound filter '{filterName}' must be placed in toolbar {{ }} or this card's filters {{ }}.");
                    }
                }
            }

            foreach (var filterName in card.LocalFilters)
            {
                if (!registry.ContainsKey(filterName))
                {
                    throw new DashSpecParseException(
                        $"Card '{card.Id}' filters block references unknown filter '{filterName}'.");
                }

                if (document.DashboardFilters.Contains(filterName, StringComparer.OrdinalIgnoreCase))
                {
                    throw new DashSpecParseException(
                        $"Filter '{filterName}' cannot be placed on dashboard and card '{card.Id}' at the same time.");
                }

                if (cardLocalOwners.TryGetValue(filterName, out var owner))
                {
                    throw new DashSpecParseException(
                        $"Filter '{filterName}' is already placed on card '{owner}'; card-local filters must be unique.");
                }

                if (registry[filterName].Kind is FilterKind.Top &&
                    card.Diagram.UsePreset is null &&
                    string.IsNullOrWhiteSpace(card.UseCardPreset) &&
                    !DiagramKindRegistry.SupportsTopLimit(card.Diagram.Kind))
                {
                    throw new DashSpecParseException(
                        $"Top filter '{filterName}' can only be placed on table cards; card '{card.Id}' uses diagram {card.Diagram.Kind}.");
                }

                cardLocalOwners[filterName] = card.Id;
            }
        }
    }
}
