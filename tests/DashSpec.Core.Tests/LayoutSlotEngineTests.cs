using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class LayoutSlotEngineTests
{
    [Fact]
    public void PlanCardInterior_matches_card_interior_slot_plan()
    {
        var placements = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["drill"] = new(2, 1, 12),
            ["day"] = new(1, 1, 4),
            ["main"] = new(2, 1, 12),
        };

        var engine = LayoutSlotEngine.PlanCardInterior(placements, ["day"], ["drill"]);
        var legacy = CardInteriorSlotPlan.Plan(placements, ["day"], ["drill"]);

        Assert.Equal(legacy.Count, engine.Count);
        for (var i = 0; i < legacy.Count; i++)
        {
            Assert.Equal(legacy[i].Token, engine[i].Token);
            Assert.Equal(legacy[i].ContentKind, engine[i].ContentKind);
            Assert.Equal(legacy[i].Scope, engine[i].Scope);
        }
    }

    [Fact]
    public void PlanHostPageToolbar_preserves_filter_order_and_scope()
    {
        var placements = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["b"] = new(1, 2, 1),
            ["a"] = new(1, 1, 1),
        };

        var plan = LayoutSlotEngine.PlanHostPageToolbar(["a", "c", "b"], placements);

        Assert.Equal(3, plan.Count);
        Assert.Equal("a", plan[0].Token);
        Assert.Equal(LayoutSlotScope.HostPageToolbar, plan[0].Scope);
        Assert.Equal(LayoutSlotContentKind.Filter, plan[0].ContentKind);
        Assert.Equal(1, plan[0].Placement.Col);
        Assert.Equal("c", plan[1].Token);
        Assert.Equal(0, plan[1].Placement.Row);
        Assert.Equal("b", plan[2].Token);
        Assert.Equal(2, plan[2].Placement.Col);
    }

    [Fact]
    public void PlanHostTabBoard_emits_card_nest_and_group_slots()
    {
        var entries = new LayoutBoardEntry[]
        {
            new LayoutBoardCardRow(["nest_a", "kpi"]),
            new LayoutBoardGroupRow(new LayoutBoardGroupDefinition("g1", "G", [["c2"]])),
        };
        var top = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["kpi"] = new(1, 2, 4),
        };
        var nests = new Dictionary<string, LayoutNestPlacement>(StringComparer.OrdinalIgnoreCase)
        {
            ["nest_a"] = new(
                "nest_a",
                new PlacementDefinition(1, 1, 8),
                [["c1"]],
                new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase) { ["c1"] = new(1, 1, 6) }),
        };
        var groups = new Dictionary<string, LayoutGroupPlacement>(StringComparer.OrdinalIgnoreCase)
        {
            ["g1"] = new("g1", "G", 2, new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase)),
        };
        var plan = new TabLayoutPlan(entries, top, groups, nests, top);

        var slots = LayoutSlotEngine.PlanHostTabBoard(plan, 12, token => token);

        Assert.Equal(3, slots.Count);
        Assert.Equal(LayoutSlotContentKind.Nest, slots[0].ContentKind);
        Assert.Equal("nest_a", slots[0].Token);
        Assert.Equal(LayoutSlotContentKind.Card, slots[1].ContentKind);
        Assert.Equal("kpi", slots[1].Token);
        Assert.Equal(LayoutSlotContentKind.Group, slots[2].ContentKind);
        Assert.Equal("g1", slots[2].Token);
        Assert.Equal(2, slots[2].Placement.Row);
    }
}
