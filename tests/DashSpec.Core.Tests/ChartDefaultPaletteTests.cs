using System.Text.RegularExpressions;
using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class ChartDefaultPaletteTests
{
    private static readonly Regex HexColor = new("^#[0-9a-f]{6}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    [Fact]
    public void Values_exposes_256_distinct_hex_colors()
    {
        var colors = ChartDefaultPalette.Values;
        Assert.Equal(256, colors.Count);
        Assert.Equal(256, colors.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(colors, color => Assert.Matches(HexColor, color));
    }

    [Fact]
    public void Resolver_draws_from_large_default_palette()
    {
        var card = new CardDefinition(
            "c",
            "C",
            new DiagramDefinition("bar", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x"] = "category",
                ["y"] = "value",
            }),
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            [],
            null,
            null,
            null,
            null);

        var colors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < 256; i++)
        {
            var colored = ChartColorResolver.ApplySeriesColors(
                [new ChartSeries($"probe_{i:x4}", [i])],
                card,
                library: null);
            colors.Add(colored[0].Color!);
        }

        Assert.True(colors.Count >= 200, $"expected wide spread, got {colors.Count} unique colors");
    }
}
