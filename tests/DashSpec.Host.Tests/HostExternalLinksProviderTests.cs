using DashSpec.Core.Model;
using DashSpec.Host.Configuration;
using DashSpec.Surface.Blazor.Services.Presentation;
using Xunit;

namespace DashSpec.Host.Tests;

public sealed class HostExternalLinksProviderTests
{
    [Fact]
    public void Load_links_from_dashhost()
    {
        var hostShell = new HostShellBootstrap
        {
            FullPath = "dashspec/demo.dashhost",
            Document = new HostDocument
            {
                Id = "demo",
                CatalogPath = "catalogs/demo.dashcatalog",
                Configuration = new Dictionary<string, string>(),
                Presentation = new Dictionary<string, string>(),
                Links =
                [
                    new HostLinkDefinition(
                        "agent_admin",
                        "Админка агента",
                        "http://localhost:5280/admin/",
                        "_blank",
                        false,
                        true),
                ],
                Surfaces = [],
                TopbarLayout = Microsoft.FSharp.Core.FSharpOption<LayoutBoardDefinition>.None,
            },
        };

        var provider = new HostExternalLinksProvider(new DashSpecHostContext
        {
            StartupRuntimeConfigPath = "runtime.toml",
            StartupRuntimeReference = "runtime.toml",
            DefaultSpecRelativePath = "spec.dashspec",
            DefaultSpecDirectory = ".",
            Catalog = null!,
            HostShell = hostShell,
        });

        var links = provider.ForStartupRuntime();
        var link = Assert.Single(links);
        Assert.Equal("Админка агента", link.Label);
        Assert.Equal("http://localhost:5280/admin/", link.Url);
        Assert.Equal("_blank", link.Target);
        Assert.False(link.Topbar);
        Assert.True(link.Settings);
    }
}
