using DashSpec.Core.Layout;

namespace DashSpec.Viz;

public static class CardInteriorSlotPlanExtensions
{
    public static IReadOnlyList<LayoutSlotDescriptor> PlanInteriorSlots(this CardRenderResult card) =>
        LayoutSlotEngine.PlanCardInterior(
            card.InteriorPlacements,
            card.LocalFilters,
            card.InteriorSlotRenders?.Keys.ToList());
}
