using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using Xunit;

namespace DashSpec.Core.Tests;

public class PlacementGridCssTests
{
    [Fact]
    public void ChromeGridStyle_uses_max_content_for_apply_column()
    {
        var placements = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["activity_slot"] = new(1, 1, 2),
            [CardLocalFilterChromeCompactor.ApplySlotId] = new(1, 3, 1),
            ["app_name"] = new(1, 4, 1),
        };

        var style = PlacementGridCss.ChromeGridStyle(placements, 4);

        Assert.Contains("max-content", style);
        Assert.Contains("--card-grid-columns:4", style);
    }
}
