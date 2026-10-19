using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class DeriveToolbarExpanderTests
{
    static readonly IReadOnlyDictionary<string, FilterDefinition> Index =
        new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["usage_date"] = FilterTestData.Create(FilterKind.Date, "usage_date", "-7d..today", "usage_date"),
            ["period_start"] = FilterTestData.Create(
                FilterKind.Date,
                "period_start",
                "today",
                "period_start",
                grainFilterName: "period_grain"),
            ["period_grain"] = FilterTestData.Create(
                FilterKind.Field,
                "period_grain",
                "day",
                "period_grain",
                widget: "combobox",
                singleSelect: true),
            ["user_name"] = FilterTestData.Create(FilterKind.Field, "user_name", null, "user_name", widget: "chips"),
        };

    [Fact]
    public void Expand_replaces_derived_target_with_source_and_grain()
    {
        var derive = new FilterDeriveDefinition("usage_date", "period_start", "period_grain");
        var visible = DeriveToolbarExpander.Expand(
            ["usage_date", "user_name"],
            derive,
            Index);

        Assert.Equal(["period_grain", "period_start", "user_name"], visible);
    }
}
