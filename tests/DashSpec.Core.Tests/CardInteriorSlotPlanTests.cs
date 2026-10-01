using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class CardInteriorSlotPlanTests
{
    [Fact]
    public void Plan_orders_by_row_then_col_and_classifies_kinds()
    {
        var plan = CardInteriorSlotPlan.Plan(
            new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["drill"] = new PlacementDefinition(2, 1, 12),
                ["day"] = new PlacementDefinition(1, 1, 4),
                ["main"] = new PlacementDefinition(2, 1, 12),
            },
            localFilters: ["day"],
            secondaryDiagramSlotRefs: ["drill"]);

        Assert.Equal(3, plan.Count);
        Assert.Equal("day", plan[0].Token);
        Assert.Equal(LayoutSlotContentKind.Filter, plan[0].ContentKind);
        Assert.Equal(LayoutSlotScope.CardInterior, plan[0].Scope);
        Assert.Equal("drill", plan[1].Token);
        Assert.Equal(LayoutSlotContentKind.SecondaryDiagram, plan[1].ContentKind);
        Assert.Equal("main", plan[2].Token);
        Assert.Equal(LayoutSlotContentKind.PrimaryDiagram, plan[2].ContentKind);
    }

    [Fact]
    public void Plan_returns_empty_when_no_interior_placements() =>
        Assert.Empty(CardInteriorSlotPlan.Plan(null, null, null));
}
