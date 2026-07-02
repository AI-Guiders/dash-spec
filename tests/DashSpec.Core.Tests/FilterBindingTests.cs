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

        Assert.Equal(
            ["peak_concurrent_proxy", "activity_5min", "dau_by_product", "peak_apps_heatmap", "idle_table"],
            map["usage_date"]);
        Assert.False(map.ContainsKey("activity_slot"));
        Assert.Equal(6, map["app_name"].Count);
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
