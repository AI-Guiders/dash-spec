using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Host.Services.Presentation;

internal static class CardFilterScopeHints
{
    public static string? ResolveTopFilterScope(CardDefinition card, DashboardDocument document)
    {
        foreach (var filterName in card.LocalFilters)
        {
            var scope = ResolveTopFilterScope(card, filterName, document);
            if (!string.IsNullOrWhiteSpace(scope))
            {
                return scope;
            }
        }

        return null;
    }

    public static string? ResolveTopFilterScope(
        CardDefinition card,
        string filterName,
        DashboardDocument document)
    {
        if (!card.LocalFilters.Contains(filterName, StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!document.Filters.Any(f =>
                string.Equals(f.Name, filterName, StringComparison.OrdinalIgnoreCase) &&
                f.Kind is FilterKind.Top))
        {
            return null;
        }

        var affected = new List<string> { card.Title };

        foreach (var other in document.Cards)
        {
            if (string.Equals(other.Id, card.Id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(other.FilterHostCardId, card.Id, StringComparison.OrdinalIgnoreCase) &&
                other.HostedFilters is not null &&
                other.HostedFilters.Contains(filterName, StringComparer.OrdinalIgnoreCase))
            {
                affected.Add(other.Title);
            }
        }

        if (affected.Count <= 1)
        {
            return null;
        }

        return affected.Count switch
        {
            2 => $"Влияет на 2 карточки: {affected[0]}, {affected[1]}",
            _ => $"Влияет на {affected.Count} карточки: {string.Join(", ", affected)}",
        };
    }
}
