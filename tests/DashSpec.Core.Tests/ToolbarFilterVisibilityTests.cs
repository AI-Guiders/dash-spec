using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Execution.Compilation;

using Xunit;



namespace DashSpec.Core.Tests;



public class ToolbarFilterVisibilityTests

{

    private const string StakeholderSpecPath = @"samples/demo\demo-stakeholder.dashspec";



    [Fact]

    public void Stakeholder_chart_top_on_page_toolbar_not_card_local()

    {

        if (!File.Exists(StakeholderSpecPath))

        {

            return;

        }



        var doc = DashSpecTestRowTypes.ParseDashboard(File.ReadAllText(StakeholderSpecPath), Path.GetDirectoryName(StakeholderSpecPath)!);

        var map = FilterBinding.MapFiltersToCards(doc);



        AssertPageToolbarHasChartTop(doc, "peak_util");

        AssertPageToolbarHasChartTop(doc, "executive_summary");

        AssertCardHasNoLocalChartTop(doc, "stakeholder_peak_over_limit");

        AssertCardHasNoLocalChartTop(doc, "stakeholder_utilization");

        AssertCardHasNoLocalChartTop(doc, "stakeholder_peak_within_limit");

        AssertCardHasNoLocalChartTop(doc, "exec_top_utilization");

        AssertCardHasNoLocalChartTop(doc, "exec_top_users_peak_apps");

        AssertCardHasNoLocalChartTop(doc, "exec_top_apps_by_users");

        AssertCardHasNoLocalChartTop(doc, "exec_top_users_work_hours");



        Assert.Contains("chart_top", map);

        Assert.Contains("exec_top_utilization", map["chart_top"]);



        var peakVisible = ToolbarFilterVisibility.ResolveVisibleFilters(

            doc,

            activeTabId: "stakeholder",

            activePageId: "peak_util",

            map);

        Assert.Contains("chart_top", peakVisible);



        var execVisible = ToolbarFilterVisibility.ResolveVisibleFilters(

            doc,

            activeTabId: "stakeholder",

            activePageId: "executive_summary",

            map);

        Assert.Contains("chart_top", execVisible);

    }



    private static void AssertPageToolbarHasChartTop(DashboardDocument doc, string pageId)

    {

        var page = doc.Pages!.Single(p => p.Id == pageId);

        var tokens = page.ToolbarBoard?.Rows.SelectMany(row => row) ?? [];

        Assert.Contains("chart_top", tokens);

    }



    private static void AssertCardHasNoLocalChartTop(DashboardDocument doc, string cardId)

    {

        var card = doc.Cards.Single(c => c.Id == cardId);

        Assert.DoesNotContain("chart_top", card.LocalFilters);

        Assert.Null(card.FilterHostCardId);

    }

}


