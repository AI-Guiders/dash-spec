using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class ToolbarFilterVisibilityTests
{
    [Fact]
    public void Stakeholder_chart_top_is_card_local_not_page_toolbar()
    {
        var path = @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec\lus-dev-stakeholder.dashspec";
        if (!File.Exists(path))
        {
            return;
        }

        var doc = DashSpecParser.Parse(File.ReadAllText(path), Path.GetDirectoryName(path)!);
        var map = FilterBinding.MapFiltersToCards(doc);
        var page = doc.Pages!.Single(p => p.Id == "peak_util");

        Assert.DoesNotContain("chart_top", page.ToolbarBoard!.Rows.SelectMany(row => row));
        Assert.Equal(["chart_top"], doc.Cards.Single(c => c.Id == "stakeholder_peak_over_limit").LocalFilters);
        Assert.Equal("stakeholder_peak_over_limit", doc.Cards.Single(c => c.Id == "stakeholder_utilization").FilterHostCardId);
        Assert.Contains("chart_top", map);
        Assert.Contains("stakeholder_peak_over_limit", map["chart_top"]);
        Assert.Contains("stakeholder_utilization", map["chart_top"]);

        var visible = ToolbarFilterVisibility.ResolveVisibleFilters(
            doc,
            activeTabId: "stakeholder",
            activePageId: "peak_util",
            map);

        Assert.DoesNotContain("chart_top", visible);
    }
}
