using DashSpec.Core.Layout;
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
            var hostedSet = new HashSet<string>(card.HostedFilters ?? [], StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(card.UseCardPreset))
            {
                var effectiveBind = CardBindResolver.Expand(
                    card.BoundFilters,
                    card.LocalFilters,
                    document.DashboardFilters);

                foreach (var filterName in effectiveBind)
                {
                    var onDashboard = document.DashboardFilters.Contains(filterName, StringComparer.OrdinalIgnoreCase);
                    var onPageToolbar = IsOnPageToolbar(document, card, filterName);
                    var onCard = localSet.Contains(filterName);
                    var onHosted = IsHostedOnCard(document, card, filterName);
                    if (!onDashboard && !onPageToolbar && !onCard && !onHosted)
                    {
                        throw new DashSpecParseException(
                            $"Card '{card.Id}': bound filter '{filterName}' must be placed in toolbar {{ }}, page toolbar, this card's filters {{ }}, or filters host <card> {{ }}.");
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

                if (hostedSet.Contains(filterName))
                {
                    throw new DashSpecParseException(
                        $"Filter '{filterName}' cannot be both local and hosted on card '{card.Id}'.");
                }

                if (cardLocalOwners.TryGetValue(filterName, out var owner))
                {
                    throw new DashSpecParseException(
                        $"Filter '{filterName}' is already placed on card '{owner}'; card-local filters must be unique.");
                }

                if (registry[filterName].Kind is FilterKind.Top)
                {
                    var hasUnresolvedPreset =
                        !string.IsNullOrWhiteSpace(card.UseCardPreset) ||
                        !string.IsNullOrWhiteSpace(card.Diagram.UsePreset);
                    var violation = TopFilterPlacementRules.GetViolation(
                        filterName,
                        card.Id,
                        card.Diagram.Kind,
                        hasUnresolvedPreset);
                    if (violation is not null)
                    {
                        throw new DashSpecParseException(violation);
                    }
                }

                cardLocalOwners[filterName] = card.Id;
            }

            foreach (var filterName in card.HostedFilters ?? [])
            {
                if (!registry.ContainsKey(filterName))
                {
                    throw new DashSpecParseException(
                        $"Card '{card.Id}' filters host block references unknown filter '{filterName}'.");
                }

                if (document.DashboardFilters.Contains(filterName, StringComparer.OrdinalIgnoreCase))
                {
                    throw new DashSpecParseException(
                        $"Filter '{filterName}' cannot be placed on dashboard and hosted on card '{card.Id}' at the same time.");
                }

                if (localSet.Contains(filterName))
                {
                    throw new DashSpecParseException(
                        $"Filter '{filterName}' cannot be both local and hosted on card '{card.Id}'.");
                }

                if (string.IsNullOrWhiteSpace(card.FilterHostCardId))
                {
                    throw new DashSpecParseException(
                        $"Card '{card.Id}' declares hosted filters without filters host <card>.");
                }

                var host = document.Cards.FirstOrDefault(other =>
                    string.Equals(other.Id, card.FilterHostCardId, StringComparison.OrdinalIgnoreCase));
                if (host is null)
                {
                    throw new DashSpecParseException(
                        $"Card '{card.Id}' filters host '{card.FilterHostCardId}' was not found.");
                }

                if (!host.LocalFilters.Contains(filterName, StringComparer.OrdinalIgnoreCase))
                {
                    throw new DashSpecParseException(
                        $"Card '{card.Id}' hosts filter '{filterName}' from '{host.Id}', but that card does not declare filters {{ {filterName} }}.");
                }
            }

            if (card.InteriorBoard is not null)
            {
                _ = CardInteriorLayoutCompactor.Compact(
                    card,
                    document.Filters,
                    document.Layout.Columns);
            }
        }
    }

    private static bool IsOnPageToolbar(DashboardDocument document, CardDefinition card, string filterName)
    {
        if (string.IsNullOrWhiteSpace(card.PageId) || document.Pages is null)
        {
            return false;
        }

        var page = document.Pages.FirstOrDefault(p =>
            string.Equals(p.Id, card.PageId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.TabId ?? "", card.TabId ?? "", StringComparison.OrdinalIgnoreCase));
        if (page?.ToolbarBoard is null)
        {
            return false;
        }

        return ToolbarPlacementResolver.ResolveFilterNames(
                document.Filters,
                [],
                page.ToolbarBoard)
            .Contains(filterName, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsHostedOnCard(
        DashboardDocument document,
        CardDefinition card,
        string filterName)
    {
        if (!(card.HostedFilters ?? []).Contains(filterName, StringComparer.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(card.FilterHostCardId))
        {
            return false;
        }

        var host = document.Cards.FirstOrDefault(other =>
            string.Equals(other.Id, card.FilterHostCardId, StringComparison.OrdinalIgnoreCase));
        return host?.LocalFilters.Contains(filterName, StringComparer.OrdinalIgnoreCase) == true;
    }
}
