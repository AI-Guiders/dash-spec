using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class KpiPriorPeriodTests
{
    [Fact]
    public void TryBuildPriorFilters_shifts_equal_length_period()
    {
        var filters = new FilterState();
        filters.SetDate("usage_date", new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 10));
        var index = new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["usage_date"] = new FilterDefinition(FilterKind.Date, "usage_date", null, "usage_date"),
        };

        Assert.True(KpiPriorPeriod.TryBuildPriorFilters(
            ["usage_date"],
            filters,
            index,
            out var prior));

        var range = prior.GetDate("usage_date");
        Assert.NotNull(range);
        Assert.Equal(new DateOnly(2026, 7, 22), range!.Value.From);
        Assert.Equal(new DateOnly(2026, 7, 31), range.Value.To);

        // Original filter state must stay intact.
        var current = filters.GetDate("usage_date");
        Assert.Equal(new DateOnly(2026, 8, 1), current!.Value.From);
        Assert.Equal(new DateOnly(2026, 8, 10), current.Value.To);
    }

    [Theory]
    [InlineData(120d, 100d, "up")]
    [InlineData(80d, 100d, "down")]
    [InlineData(100d, 100d, "flat")]
    public void FormatDelta_tone_follows_direction(double current, double prior, string tone)
    {
        var (_, actualTone) = KpiPriorPeriod.FormatDelta(current, prior);
        Assert.Equal(tone, actualTone);
    }

    [Fact]
    public void WantsPriorDelta_accepts_aliases()
    {
        Assert.True(KpiPriorPeriod.WantsPriorDelta(new DiagramDefinition(
            "number",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["delta"] = "prior" })));
        Assert.True(KpiPriorPeriod.WantsPriorDelta(new DiagramDefinition(
            "number",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["delta"] = "previous" })));
        Assert.False(KpiPriorPeriod.WantsPriorDelta(new DiagramDefinition(
            "number",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["delta"] = "none" })));
    }
}
