using DashSpec.Core.Parsing;
using DashSpec.Core.Layout;
using DashSpec.Execution.Compilation;
using Xunit;

namespace DashSpec.Core.Tests;

public class DemoStakeholderSyntaxTests
{
    [Fact]
    public void Parse_demo_stakeholder_canonical_syntax()
    {
        var path = @"samples/demo\demo-stakeholder.dashspec";
        if (!File.Exists(path))
        {
            return;
        }

        var text = File.ReadAllText(path);
        var doc = DashSpecParser.Parse(text, Path.GetDirectoryName(path)!);

        Assert.Equal(6, doc.Filters.Count);
        Assert.True(doc.Cards.Count >= 20, $"cards={doc.Cards.Count}");
        Assert.Contains(doc.Cards, c => c.Id == "exec_top_utilization");
        Assert.Contains(doc.Cards, c => c.Id == "exec_kpi_total_users");
        Assert.Equal("chart_top", doc.Filters.Single(f => f.Kind == Model.FilterKind.Top).Name);
        Assert.Equal("usage_date", doc.Filters[0].Name);
        Assert.Equal("Дата отчёта", doc.Filters[0].Label);
        Assert.Null(doc.Cards.Single(c => c.Id == "stakeholder_peak_apps_browse").SeriesTransform);

        var execPage = doc.Pages!.Single(p => p.Id == "executive_summary");
        var execToolbar = execPage.ToolbarBoard!.Rows.SelectMany(row => row).ToList();
        Assert.Contains("chart_top", execToolbar);

        foreach (var cardId in new[]
                 {
                     "exec_top_utilization",
                     "exec_top_users_peak_apps",
                     "exec_top_apps_by_users",
                     "exec_top_users_work_hours",
                 })
        {
            Assert.DoesNotContain("chart_top", doc.Cards.Single(c => c.Id == cardId).LocalFilters);
        }

        var map = FilterBinding.MapFiltersToCards(doc);
        Assert.Contains("chart_top", ToolbarFilterVisibility.ResolveVisibleFilters(
            doc,
            activeTabId: "stakeholder",
            activePageId: "executive_summary",
            map));
    }
}
