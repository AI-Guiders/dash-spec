using DashSpec.Abstractions.Query;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class FilterBindingTests
{
    [Fact]
    public void MapFiltersToCards_activity_day_only_on_activity_card()
    {
        var soakPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "samples", "demo",
            "demo-soak.dashspec"));
        var specDir = Path.GetDirectoryName(soakPath)!;

        var doc = DashSpecParser.Parse(File.ReadAllText(soakPath), specDir);

        SpecLibrary? library = null;

        var map = FilterBinding.MapFiltersToCards(doc, library);

        Assert.Contains("peak_concurrent_proxy", map["usage_date"]);
        Assert.Contains("activity_5min", map["usage_date"]);
        Assert.Contains("dau_by_product", map["usage_date"]);
        Assert.Contains("peak_apps_heatmap", map["usage_date"]);
        Assert.Contains("idle_table", map["usage_date"]);
        Assert.True(map["usage_date"].Count >= 5);
        Assert.Equal(["activity_5min"], map["activity_slot"]);
        Assert.True(map["app_name"].Count >= 6);
        Assert.Equal(["activity_slot"], doc.Cards.Single(c => c.Id == "activity_5min").LocalFilters);
        Assert.Equal(
            ["usage_date", "user_name", "app_name", "activity_slot"],
            CardResolver.ResolveCard(
                doc.Cards.Single(c => c.Id == "activity_5min"),
                library,
                doc.DashboardFilters).BoundFilters);
        Assert.Equal(["events_top"], doc.Cards.Single(c => c.Id == "events_detail").LocalFilters);
        Assert.Equal(["idle_top"], doc.Cards.Single(c => c.Id == "idle_table").LocalFilters);
    }
}
