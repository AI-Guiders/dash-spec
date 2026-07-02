using DashSpec.Core.Analysis;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;

namespace DashSpec.Core.Resolution;

public sealed record ResolvedDashboard(
    DashboardDocument Parsed,
    SpecLibrary? Library,
    IReadOnlyList<ResolvedCardView> Cards);

public static class SpecResolver
{
    public static ResolvedDashboard Resolve(DashboardDocument parsed, SpecLibrary? library)
    {
        ArgumentNullException.ThrowIfNull(parsed);

        var cards = parsed.Cards
            .Select(card => CardResolver.Resolve(card, library, parsed.DashboardFilters))
            .ToList();

        CardSemanticValidator.Validate(parsed, cards);
        return new ResolvedDashboard(parsed, library, cards);
    }

    public static ResolvedCardView ResolveCard(
        DashboardDocument parsed,
        SpecLibrary? library,
        string cardId)
    {
        var card = parsed.Cards.Single(c => string.Equals(c.Id, cardId, StringComparison.OrdinalIgnoreCase));
        return CardResolver.Resolve(card, library, parsed.DashboardFilters);
    }
}

internal static class CardSemanticValidator
{
    public static void Validate(DashboardDocument document, IReadOnlyList<ResolvedCardView> resolvedCards)
    {
        for (var i = 0; i < document.Cards.Count; i++)
        {
            var card = document.Cards[i];
            var effective = resolvedCards[i].Card;

            foreach (var filterName in card.LocalFilters)
            {
                var filter = document.Filters.Single(f =>
                    string.Equals(f.Name, filterName, StringComparison.OrdinalIgnoreCase));
                if (filter.Kind is FilterKind.Top)
                {
                    var violation = TopFilterPlacementRules.GetViolation(
                        filterName,
                        card.Id,
                        effective.Diagram.Kind,
                        hasUnresolvedPreset: false);
                    if (violation is not null)
                    {
                        throw new InvalidOperationException(violation);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(effective.DataSource.Value))
            {
                throw new InvalidOperationException($"Card '{card.Id}' has no datasource after resolving presets.");
            }
        }
    }
}
