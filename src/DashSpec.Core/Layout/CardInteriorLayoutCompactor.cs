using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

/// <summary>Card interior grid: diagram slots + local filters from bracket board.</summary>
public static class CardInteriorLayoutCompactor
{
    public static IReadOnlyDictionary<string, PlacementDefinition> Compact(
        CardDefinition card,
        IReadOnlyList<FilterDefinition> filters,
        int columns)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(filters);
        if (columns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }

        var board = card.InteriorBoard ?? CardInteriorLayoutDefaults.Synthesize(card);

        var context = $"Card '{card.Id}' interior";
        var placements = LayoutBoardPlacer.Resolve(
            board,
            columns,
            context,
            token => CardInteriorSlotResolver.Resolve(token, card, filters));

        ValidateCoverage(card, placements, context);
        return placements;
    }

    private static void ValidateCoverage(
        CardDefinition card,
        IReadOnlyDictionary<string, PlacementDefinition> placements,
        string context)
    {
        var slots = CardDiagramSlotCatalog.ResolveSlots(card);
        foreach (var slotRef in slots.Keys)
        {
            if (!placements.ContainsKey(slotRef))
            {
                var token = string.Equals(slotRef, CardInteriorSlots.Diagram, StringComparison.Ordinal)
                    ? "diagram"
                    : slotRef;
                throw new DashSpecParseException(
                    $"{context}: layout board must include diagram slot '{token}'.");
            }
        }

        var allowed = new HashSet<string>(card.LocalFilters, StringComparer.OrdinalIgnoreCase);
        foreach (var slotRef in slots.Keys)
        {
            allowed.Add(slotRef);
        }

        foreach (var slot in placements.Keys)
        {
            if (!allowed.Contains(slot))
            {
                throw new DashSpecParseException(
                    $"{context}: layout token resolves to unknown slot '{slot}'.");
            }
        }
    }
}
