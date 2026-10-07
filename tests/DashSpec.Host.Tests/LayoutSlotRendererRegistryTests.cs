using DashSpec.Core.Layout;
using DashSpec.Surface.Blazor.Services.Presentation;
using Xunit;

namespace DashSpec.Host.Tests;

public sealed class LayoutSlotRendererRegistryTests
{
    [Fact]
    public void Supports_all_host_slot_plane_pairs()
    {
        var registry = new LayoutSlotRendererRegistry();
        foreach (var (scope, kind) in registry.AllSupported())
        {
            Assert.True(registry.Supports(scope, kind));
        }

        Assert.False(registry.Supports(LayoutSlotScope.HostTabBoard, LayoutSlotContentKind.PrimaryDiagram));
    }
}
