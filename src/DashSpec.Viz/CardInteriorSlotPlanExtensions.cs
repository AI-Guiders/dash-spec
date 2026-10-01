using DashSpec.Core.Layout;

namespace DashSpec.Viz;

public static class CardInteriorSlotPlanExtensions
{
    public static IReadOnlyList<LayoutSlotDescriptor> PlanInteriorSlots(this CardRenderResult card) =>
        CardInteriorSlotPlan.Plan(
            card.InteriorPlacements,
            card.LocalFilters,
            card.InteriorSlotRenders?.Keys.ToList());
}
