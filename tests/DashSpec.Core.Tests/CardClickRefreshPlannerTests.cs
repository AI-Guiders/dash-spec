using DashSpec.Core.Model;
using Xunit;

namespace DashSpec.Core.Tests;

public class CardClickRefreshPlannerTests
{
    [Fact]
    public void Plan_drill_table_from_cell_refreshes_interior_slots_only()
    {
        var effects = new CardClickEffect[]
        {
            new DrillTableFromCellEffect(
            [
                new SetFilterFromFieldEffect("usage_date", "x"),
                new SetFilterFromFieldEffect("app_name", "y"),
            ]),
        };

        Assert.Equal(CardInteractionRefresh.CardInteriorSlots, CardClickRefreshPlanner.Plan(effects));
    }

    [Fact]
    public void Plan_set_filter_refreshes_report()
    {
        var effects = new CardClickEffect[]
        {
            new SetFilterFromFieldEffect("app_name", "y"),
        };

        Assert.Equal(CardInteractionRefresh.Report, CardClickRefreshPlanner.Plan(effects));
    }

    [Fact]
    public void Plan_show_only_does_not_refresh()
    {
        var effects = new CardClickEffect[]
        {
            new ShowSelectionEffect(ShowPlacement.Below, ShowFormat.List, ShowSource.Tooltip, CopyFriendly: true),
        };

        Assert.Equal(CardInteractionRefresh.None, CardClickRefreshPlanner.Plan(effects));
    }
}
