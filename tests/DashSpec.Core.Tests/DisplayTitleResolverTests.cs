using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DisplayTitleResolverTests
{
    private static readonly FilterDefinition ChartTop = new(
        FilterKind.Top,
        "chart_top",
        "10",
        "chart_top",
        MinValue: 0,
        MaxValue: 50);

    private static readonly FilterDefinition PeriodStart = new(
        FilterKind.Date,
        "period_start",
        "today",
        "period_start",
        GrainFilterName: "period_grain");

    [Fact]
    public void DisplayTemplate_collects_slots()
    {
        Assert.Equal(["top", "range"], DisplayTemplate.CollectSlots("Топ {top} · {range}"));
    }

    [Fact]
    public void Resolve_uses_bind_display_slot_for_top_filter()
    {
        var context = new FilterDisplayContext(
            new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                [ChartTop.Name] = ChartTop,
            },
            new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [ChartTop.Name] = 25,
            });

        var bindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["top"] = "chart_top.value",
        };

        var title = DisplayTitleResolver.Resolve("Топ {top}", bindings, context);
        Assert.Equal("Топ 25", title);
    }

    [Fact]
    public void Resolve_top_zero_shows_all_label()
    {
        var context = new FilterDisplayContext(
            new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                [ChartTop.Name] = ChartTop,
            },
            new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                [ChartTop.Name] = 0,
            });

        var bindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["top"] = "chart_top",
        };

        var title = DisplayTitleResolver.Resolve("Топ {top}", bindings, context);
        Assert.Equal("Топ все", title);
    }

    [Fact]
    public void Resolve_date_range_binding()
    {
        var from = new DateOnly(2026, 9, 1);
        var to = new DateOnly(2026, 9, 25);
        var context = new FilterDisplayContext(
            new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                [PeriodStart.Name] = PeriodStart,
            },
            new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase)
            {
                [PeriodStart.Name] = from,
            },
            new Dictionary<string, DateOnly>(StringComparer.OrdinalIgnoreCase)
            {
                [PeriodStart.Name] = to,
            },
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

        var bindings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["range"] = "period_start.range",
        };

        var title = DisplayTitleResolver.Resolve("Период {range}", bindings, context);
        Assert.Equal("Период 01.09…25.09", title);
    }
}
