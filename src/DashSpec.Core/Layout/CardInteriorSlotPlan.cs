using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

/// <summary>Card-interior slot plan (ADR-0072 M1); delegates to <see cref="LayoutSlotEngine"/>.</summary>
public static class CardInteriorSlotPlan
{
    public static IReadOnlyList<LayoutSlotDescriptor> Plan(
        IReadOnlyDictionary<string, PlacementDefinition>? interiorPlacements,
        IReadOnlyList<string>? localFilters,
        IReadOnlyCollection<string>? secondaryDiagramSlotRefs) =>
        LayoutSlotEngine.PlanCardInterior(interiorPlacements, localFilters, secondaryDiagramSlotRefs);
}
