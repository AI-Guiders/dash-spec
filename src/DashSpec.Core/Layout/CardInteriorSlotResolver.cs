using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

internal static class CardInteriorSlotResolver
{
    public static string Resolve(string token, CardDefinition card, IReadOnlyList<FilterDefinition> filters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentNullException.ThrowIfNull(card);

        if (string.Equals(token, "diagram", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(card.DiagramSlotRef))
            {
                throw new DashSpecParseException(
                    $"Card '{card.Id}' interior: use diagram ref '{card.DiagramSlotRef}' instead of reserved token 'diagram'.");
            }

            return CardInteriorSlots.Diagram;
        }

        if (!string.IsNullOrWhiteSpace(card.DiagramSlotRef) &&
            string.Equals(token, card.DiagramSlotRef, StringComparison.OrdinalIgnoreCase))
        {
            return CardInteriorSlots.Diagram;
        }

        var localFilterDefs = new List<FilterDefinition>();
        var filterRegistry = filters.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var name in card.LocalFilters)
        {
            if (!filterRegistry.TryGetValue(name, out var filter))
            {
                throw new DashSpecParseException(
                    $"Card '{card.Id}' interior: unknown local filter '{name}'.");
            }

            localFilterDefs.Add(filter);
        }

        var filterName = FilterLayoutRefResolver.Resolve(
            token,
            localFilterDefs,
            $"Card '{card.Id}' interior");

        if (!card.LocalFilters.Contains(filterName, StringComparer.OrdinalIgnoreCase))
        {
            throw new DashSpecParseException(
                $"Card '{card.Id}' interior: token '{token}' resolves to filter '{filterName}' which is not local on this card.");
        }

        return filterName;
    }
}
