using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class LusStakeholderSyntaxTests
{
    [Fact]
    public void Parse_lus_dev_stakeholder_canonical_syntax()
    {
        var path = @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec\lus-dev-stakeholder.dashspec";
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
        Assert.Equal(25, doc.Cards.Single(c => c.Id == "stakeholder_peak_apps_browse").SeriesTransform?.Max);
    }
}
