using DashSpec.Core.Model;
using Xunit;
using DashSpec.Execution.Runtime;

namespace DashSpec.Core.Tests;

public class CardClickRefreshPlannerTests
{
    [Fact]
    public void Plan_drill_table_from_cell_refreshes_interior_slots_only()
    {
        var effects = new CardClickEffect[]
        {
            CardClickEffect.NewDrillTableFromCell(
                new[]
                {
                    System.Tuple.Create("usage_date", "x"),
                    System.Tuple.Create("app_name", "y"),
                }),
        };

        Assert.Equal(CardInteractionRefresh.CardInteriorSlots, CardClickRefreshPlanner.Plan(effects));
    }

    [Fact]
    public void Plan_set_filter_refreshes_report()
    {
        var effects = new CardClickEffect[]
        {
            CardClickEffect.NewSetFilterFromField("app_name", "y"),
        };

        Assert.Equal(CardInteractionRefresh.Report, CardClickRefreshPlanner.Plan(effects));
    }

    [Fact]
    public void Plan_show_only_does_not_refresh()
    {
        var effects = new CardClickEffect[]
        {
            CardClickEffect.NewShowSelection(ShowPlacement.Below, ShowFormat.List, ShowSource.Tooltip, true, Microsoft.FSharp.Core.FSharpOption<string>.None),
        };

        Assert.Equal(CardInteractionRefresh.None, CardClickRefreshPlanner.Plan(effects));
    }
}
