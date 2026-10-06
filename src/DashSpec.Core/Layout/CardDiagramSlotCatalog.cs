using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

public static class CardDiagramSlotCatalog
{
    public static IReadOnlyDictionary<string, CardDiagramSlotDefinition> ResolveSlots(CardDefinition card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card.DiagramSlots is { Count: > 0 })
        {
            return card.DiagramSlots;
        }

        return new Dictionary<string, CardDiagramSlotDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [ResolvePrimarySlotRef(card)] = new CardDiagramSlotDefinition(
                ResolvePrimarySlotRef(card),
                card.Diagram,
                card.DataSource,
                card.BoundFilters,
                card.Legend,
                card.Presentation,
                card.SeriesTransform,
                card.FlowInput),
        };
    }

    public static string ResolvePrimarySlotRef(CardDefinition card)
    {
        if (!string.IsNullOrWhiteSpace(card.DiagramSlotRef))
        {
            return card.DiagramSlotRef;
        }

        return CardInteriorSlots.Diagram;
    }

    public static bool IsDiagramSlotToken(CardDefinition card, string token)
    {
        var slots = ResolveSlots(card);
        return slots.ContainsKey(token) ||
               (string.Equals(token, "diagram", StringComparison.OrdinalIgnoreCase) &&
                slots.ContainsKey(CardInteriorSlots.Diagram));
    }
}
