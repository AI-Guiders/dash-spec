using DashSpec.Viz;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class VizCardDisplayActionsTests
{
    [Theory]
    [InlineData(VizCardDisplayActions.LegacyToggleAxisLabelsY, VizDisplayToggleKind.AxisLabelsY)]
    [InlineData(VizCardDisplayActions.ToggleAxisLabelsY, VizDisplayToggleKind.AxisLabelsY)]
    public void TryMap_accepts_legacy_and_canonical_ids(string actionId, VizDisplayToggleKind expected)
    {
        Assert.True(VizCardDisplayActions.TryMap(actionId, out var kind));
        Assert.Equal(expected, kind);
    }
}
