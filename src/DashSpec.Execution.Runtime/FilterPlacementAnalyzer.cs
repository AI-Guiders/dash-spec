using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;

namespace DashSpec.Execution.Runtime;

internal static class FilterPlacementAnalyzer
{
    public static void Validate(DashboardDocument document)
    {
        var errors = new List<string>();
        CollectErrors(document, errors);
        if (errors.Count > 0)
        {
            throw new DashSpecParseException(errors[0]);
        }
    }

    internal static void CollectErrors(DashboardDocument document, List<string> errors)
    {
        var registry = document.Filters.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var filterName in document.DashboardFilters)
        {
            if (!registry.ContainsKey(filterName))
            {
                errors.Add($"Dashboard filters block references unknown filter '{filterName}'.");
                continue;
            }

            if (registry[filterName].Kind == FilterKind.Top)
            {
                errors.Add($"Top filter '{filterName}' cannot be placed in toolbar; use card filters {{ }}.");
            }
        }

        foreach (var card in document.Cards)
        {
            var localSet = new HashSet<string>(card.LocalFilters, StringComparer.OrdinalIgnoreCase);
            var hostedSet = new HashSet<string>(card.HostedFilters?.Value ?? [], StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(card.UseCardPreset?.Value))
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
                        errors.Add(
                            $"Card '{card.Id}': bound filter '{filterName}' must be placed in toolbar {{ }}, page toolbar, this card's filters {{ }}, or filters host <card> {{ }}.");
                    }
                }
            }

            foreach (var filterName in card.LocalFilters)
            {
                if (!registry.ContainsKey(filterName))
                {
                    errors.Add($"Card '{card.Id}' filters block references unknown filter '{filterName}'.");
                    continue;
                }

                if (document.DashboardFilters.Contains(filterName, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"Filter '{filterName}' cannot be placed on dashboard and card '{card.Id}' at the same time.");
                }

                if (hostedSet.Contains(filterName))
                {
                    errors.Add(
                        $"Filter '{filterName}' cannot be both local and hosted on card '{card.Id}'.");
                }

                if (registry[filterName].Kind == FilterKind.Top)
                {
                    var hasUnresolvedPreset =
                        !string.IsNullOrWhiteSpace(card.UseCardPreset?.Value) ||
                        !string.IsNullOrWhiteSpace(card.Diagram.UsePreset?.Value);
                    var violation = TopFilterPlacementRules.GetViolation(
                        filterName,
                        card.Id,
                        card.Diagram.Kind,
                        hasUnresolvedPreset);
                    if (violation is not null)
                    {
                        errors.Add(violation);
                    }
                }
            }

            foreach (var filterName in card.HostedFilters?.Value ?? [])
            {
                if (!registry.ContainsKey(filterName))
                {
                    errors.Add($"Card '{card.Id}' filters host block references unknown filter '{filterName}'.");
                    continue;
                }

                if (document.DashboardFilters.Contains(filterName, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"Filter '{filterName}' cannot be placed on dashboard and hosted on card '{card.Id}' at the same time.");
                }

                if (localSet.Contains(filterName))
                {
                    errors.Add(
                        $"Filter '{filterName}' cannot be both local and hosted on card '{card.Id}'.");
                }

                if (string.IsNullOrWhiteSpace(card.FilterHostCardId?.Value))
                {
                    errors.Add($"Card '{card.Id}' declares hosted filters without filters host <card>.");
                    continue;
                }

                var host = document.Cards.FirstOrDefault(other =>
                    string.Equals(other.Id, card.FilterHostCardId?.Value, StringComparison.OrdinalIgnoreCase));
                if (host is null)
                {
                    errors.Add($"Card '{card.Id}' filters host '{card.FilterHostCardId?.Value}' was not found.");
                    continue;
                }

                if (!host.LocalFilters.Contains(filterName, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add(
                        $"Card '{card.Id}' hosts filter '{filterName}' from '{host.Id}', but that card does not declare filters {{ {filterName} }}.");
                }
            }

            try
            {
                _ = CardInteriorLayoutCompactor.Compact(
                    card,
                    document.Filters,
                    document.Layout.Columns);
            }
            catch (DashSpecParseException ex)
            {
                errors.Add(ex.Message);
            }
        }
    }

    private static bool IsOnPageToolbar(DashboardDocument document, CardDefinition card, string filterName)
    {
        if (string.IsNullOrWhiteSpace(card.PageId?.Value) || document.Pages?.Value is null)
        {
            return false;
        }

        var page = document.Pages.Value.FirstOrDefault(p =>
            string.Equals(p.Id, card.PageId?.Value, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.TabId?.Value ?? "", card.TabId?.Value ?? "", StringComparison.OrdinalIgnoreCase));
        if (page?.ToolbarBoard is null)
        {
            return false;
        }

        try
        {
            return DashSpec.Modeling.Parse.Document.ToolbarPlacementResolver.resolveFilterNames(
                    document.Filters,
                    [],
                    Microsoft.FSharp.Core.FSharpOption<DashSpec.Modeling.Parse.Layout.LayoutBoardDefinition>.Some(page.ToolbarBoard.Value))
                .Contains(filterName, StringComparer.OrdinalIgnoreCase);
        }
        catch (DashSpec.Modeling.Core.DashSpecParseException ex)
        {
            throw new DashSpecParseException(ex.Message, ex.SourceOffset);
        }
    }

    private static bool IsHostedOnCard(
        DashboardDocument document,
        CardDefinition card,
        string filterName)
    {
        if (!(card.HostedFilters?.Value ?? []).Contains(filterName, StringComparer.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(card.FilterHostCardId?.Value))
        {
            return false;
        }

        var host = document.Cards.FirstOrDefault(other =>
            string.Equals(other.Id, card.FilterHostCardId?.Value, StringComparison.OrdinalIgnoreCase));
        return host?.LocalFilters.Contains(filterName, StringComparer.OrdinalIgnoreCase) == true;
    }
}
