using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class HeatmapColorScaleTests
{
    [Fact]
    public void CellBackground_degenerate_range_uses_scale_not_flat_blue()
    {
        var color = HeatmapColorScale.CellBackground("heat", 1d, 1d, 1d);
        Assert.DoesNotContain("80%, 48%", color, StringComparison.Ordinal);
        Assert.StartsWith("hsl(", color, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0d, 0d, 0d)]
    [InlineData(5d, 5d, 5d)]
    public void CellBackground_degenerate_range_maps_single_value_to_scale_end(double value, double min, double max)
    {
        var atMin = HeatmapColorScale.CellBackground("heat", min, min, max);
        var atValue = HeatmapColorScale.CellBackground("heat", value, min, max);
        Assert.Equal(atMin, atValue);
    }
}
