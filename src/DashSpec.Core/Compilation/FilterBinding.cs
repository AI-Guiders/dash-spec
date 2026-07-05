using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;

namespace DashSpec.Core.Compilation;

public static class FilterBinding
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> MapCardsToFilters(
        DashboardDocument document,
        SpecLibrary? library = null) =>
        document.Cards.ToDictionary(
            card => card.Id,
            card => ResolveBoundFilters(card, document, library),
            StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> MapFiltersToCards(
        DashboardDocument document,
        SpecLibrary? library = null)
    {
        var result = document.DashboardFilters.ToDictionary(
            filterName => filterName,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (var card in document.Cards)
        {
            foreach (var filterName in ResolveBoundFilters(card, document, library))
            {
                if (!document.DashboardFilters.Contains(filterName, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (result.TryGetValue(filterName, out var cards) &&
                    !cards.Contains(card.Id, StringComparer.OrdinalIgnoreCase))
                {
                    cards.Add(card.Id);
                }
            }
        }

        return result.ToDictionary(
            x => x.Key,
            x => (IReadOnlyList<string>)x.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ResolveBoundFilters(
        CardDefinition card,
        DashboardDocument document,
        SpecLibrary? library)
    {
        if (string.IsNullOrWhiteSpace(card.UseCardPreset) && library is null)
        {
            return CardBindResolver.Expand(
                card.BoundFilters,
                card.LocalFilters,
                document.DashboardFilters);
        }

        return CardResolver.ResolveCard(card, library, document.DashboardFilters).BoundFilters;
    }
}
