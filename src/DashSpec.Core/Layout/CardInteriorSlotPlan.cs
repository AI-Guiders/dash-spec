using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

/// <summary>Builds ordered card-interior slot descriptors for host dispatch (ADR-0072 M1).</summary>
public static class CardInteriorSlotPlan
{
    public static IReadOnlyList<LayoutSlotDescriptor> Plan(
        IReadOnlyDictionary<string, PlacementDefinition>? interiorPlacements,
        IReadOnlyList<string>? localFilters,
        IReadOnlyCollection<string>? secondaryDiagramSlotRefs)
    {
        if (interiorPlacements is not { Count: > 0 })
        {
            return [];
        }

        var secondary = secondaryDiagramSlotRefs is null
            ? null
            : new HashSet<string>(secondaryDiagramSlotRefs, StringComparer.OrdinalIgnoreCase);

        return interiorPlacements
            .OrderBy(static kv => kv.Value.Row)
            .ThenBy(static kv => kv.Value.Col)
            .Select(kv => new LayoutSlotDescriptor(
                kv.Key,
                kv.Value,
                LayoutSlotScope.CardInterior,
                Classify(kv.Key, localFilters, secondary)))
            .ToList();
    }

    internal static LayoutSlotContentKind Classify(
        string token,
        IReadOnlyList<string>? localFilters,
        IReadOnlySet<string>? secondaryDiagramSlots)
    {
        if (localFilters?.Contains(token, StringComparer.OrdinalIgnoreCase) == true)
        {
            return LayoutSlotContentKind.Filter;
        }

        if (secondaryDiagramSlots?.Contains(token) == true)
        {
            return LayoutSlotContentKind.SecondaryDiagram;
        }

        return LayoutSlotContentKind.PrimaryDiagram;
    }
}
