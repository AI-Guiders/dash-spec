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
    public void ResolveLabelColors_uses_round_robin_for_category_labels()
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

        var labels = Enumerable.Range(0, 256).Select(i => $"app_{i}").ToList();
        var colors = ChartColorResolver.ResolveLabelColors(labels, card, library: null, dashboardColorPalette: null);
        Assert.Equal(256, colors.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
