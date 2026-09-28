using DashSpec.Host.Services.Presentation;
using Xunit;

namespace DashSpec.Host.Tests;

public sealed class CardMatrixDisplayStateTests
{
    [Fact]
    public void ToggleAxisLabelsY_raises_changed_for_card()
    {
        var service = new CardMatrixDisplayStateService();
        string? changedId = null;
        service.Changed += id => changedId = id;

        service.ToggleAxisLabelsY("heatmap-1", currentlyVisible: true);

        Assert.Equal("heatmap-1", changedId);
        Assert.False(service.GetAxisLabelsYOverride("heatmap-1"));
    }
}
