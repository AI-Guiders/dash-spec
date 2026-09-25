using DashSpec.Core.Model;
using DashSpec.Host.Services.Presentation;
using Xunit;

namespace DashSpec.Host.Tests;

public sealed class HostTopbarLayoutResolverTests
{
    [Fact]
    public void Resolve_flattens_scope_host_board()
    {
        var board = new LayoutBoardDefinition(
            [
                ["catalog"],
                ["nav", "external_links"],
                ["help", "settings"],
            ],
            LayoutScope.Host);

        var slots = HostTopbarLayoutResolver.Resolve(board);

        Assert.Equal(
            [
                HostTopbarSlots.Catalog,
                HostTopbarSlots.Nav,
                HostTopbarSlots.ExternalLinks,
                HostTopbarSlots.Help,
                HostTopbarSlots.Settings,
            ],
            slots);
    }

    [Fact]
    public void Resolve_normalizes_report_picker_and_dashboard_aliases()
    {
        var board = new LayoutBoardDefinition(
            [["report_picker", "dashboard"]],
            LayoutScope.Host);

        var slots = HostTopbarLayoutResolver.Resolve(board);

        Assert.Equal([HostTopbarSlots.Catalog, HostTopbarSlots.Nav], slots);
    }
}
