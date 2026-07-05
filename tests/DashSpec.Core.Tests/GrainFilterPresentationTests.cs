using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class GrainFilterPresentationTests
{
    private static readonly IReadOnlyDictionary<string, FilterDefinition> Index =
        new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["period_grain"] = new(FilterKind.Field, "period_grain", "day", "lus.v.period_grain"),
            ["period_start"] = new(
                FilterKind.Date,
                "period_start",
                "today..today",
                "period_start",
                Label: "Период",
                Widget: "day",
                GrainFilterName: "period_grain",
                GrainLabels: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["month"] = "Месяц отчёта",
                }),
        };

    [Theory]
    [InlineData("day", "День")]
    [InlineData("month", "Месяц отчёта")]
    [InlineData("year", "Год")]
    public void DisplayLabel_uses_grain_specific_labels(string grain, string expected)
    {
        var selected = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["period_grain"] = new HashSet<string>([grain], StringComparer.OrdinalIgnoreCase),
        };

        var label = GrainFilterPresentation.DisplayLabel(Index["period_start"], Index, selected);

        Assert.Equal(expected, label);
    }

    [Theory]
    [InlineData("month", "2026-06-24", "2026-06-01")]
    [InlineData("year", "2026-06-24", "2026-01-01")]
    [InlineData("day", "2026-06-24", "2026-06-24")]
    public void NormalizeAnchor_matches_sql_compiler(string grain, string input, string expected)
    {
        var selected = DateOnly.Parse(input);
        var anchor = GrainFilterPresentation.NormalizeAnchor(selected, grain);

        Assert.Equal(DateOnly.Parse(expected), anchor);
    }

    [Theory]
    [InlineData("month", "2026-06-01", "2026-06")]
    [InlineData("year", "2026-01-01", "2026")]
    [InlineData("day", "2026-06-24", "2026-06-24")]
    public void FormatChipValue_shows_matching_part(string grain, string date, string expected)
    {
        var day = DateOnly.Parse(date);
        var value = GrainFilterPresentation.FormatChipValue(day, day, grain);

        Assert.Equal(expected, value);
    }

    [Fact]
    public void SnapAnchoredDates_uses_reference_day_for_each_grain()
    {
        var dateFrom = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase)
        {
            ["period_start"] = new DateOnly(2026, 1, 1),
        };
        var dateTo = new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase)
        {
            ["period_start"] = new DateOnly(2026, 1, 1),
        };
        var selected = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["period_grain"] = new HashSet<string>(["month"], StringComparer.OrdinalIgnoreCase),
        };

        GrainFilterPresentation.SnapAnchoredDates(
            "period_grain",
            Index,
            selected,
            dateFrom,
            dateTo,
            new DateOnly(2026, 6, 24));

        Assert.Equal(new DateOnly(2026, 6, 1), dateFrom["period_start"]);
        Assert.Equal(new DateOnly(2026, 6, 1), dateTo["period_start"]);
    }
}
