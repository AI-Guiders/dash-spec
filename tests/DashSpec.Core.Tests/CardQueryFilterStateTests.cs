using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class CardQueryFilterStateTests
{
    [Fact]
    public void Compose_overrides_session_with_card_local_values_only_for_that_card()
    {
        var session = new FilterState();
        session.SetField("app_name", ["OverviewApp"]);
        session.SetDate("usage_date", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 7));

        var card = new CardDefinition(
            "versions_card",
            "T",
            default!,
            default!,
            ["usage_date", "app_name"],
            LocalFilters: ["usage_date", "app_name"]);

        var index = new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["usage_date"] = new(FilterKind.Date, "usage_date", "-7d..today", "usage_date"),
            ["app_name"] = new(FilterKind.Field, "app_name", null, "app_name"),
        };

        var localDates = new Dictionary<string, DateRangeValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["usage_date"] = new(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 1)),
        };
        var localFields = new Dictionary<string, FieldFilterValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["app_name"] = new(["VersionsApp"]),
        };

        var composed = CardQueryFilterState.Compose(session, card, index, localDates, localFields, null);

        Assert.Equal(new DateOnly(2026, 3, 1), composed.GetDate("usage_date")!.Value.From);
        Assert.Equal(["VersionsApp"], composed.GetField("app_name")!.Value.Values);
    }
}
