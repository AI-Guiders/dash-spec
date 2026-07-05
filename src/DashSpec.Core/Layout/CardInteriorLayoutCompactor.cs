using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Layout;

/// <summary>Card interior grid: diagram slot + local filters from bracket board.</summary>
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

        if (card.InteriorBoard is null)
        {
            return new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        var context = $"Card '{card.Id}' interior";
        var placements = LayoutBoardPlacer.Resolve(
            card.InteriorBoard,
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
        if (!placements.ContainsKey(CardInteriorSlots.Diagram))
        {
            throw new DashSpecParseException(
                $"{context}: layout board must include the diagram slot " +
                $"(token '{card.DiagramSlotRef ?? "diagram"}').");
        }

        foreach (var filterName in card.LocalFilters)
        {
            if (!placements.ContainsKey(filterName))
            {
                throw new DashSpecParseException(
                    $"{context}: layout board must include local filter '{filterName}'.");
            }
        }

        var allowed = new HashSet<string>(card.LocalFilters, StringComparer.OrdinalIgnoreCase)
        {
            CardInteriorSlots.Diagram,
        };

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
