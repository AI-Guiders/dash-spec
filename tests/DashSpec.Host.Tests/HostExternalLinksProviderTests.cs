using DashSpec.Core.Model;
using DashSpec.Host.Configuration;
using DashSpec.Host.Services.Presentation;
using Xunit;

namespace DashSpec.Host.Tests;

public sealed class HostExternalLinksProviderTests
{
    [Fact]
    public void Load_links_from_dashhost()
    {
        var hostShell = new HostShellBootstrap
        {
            FullPath = "dashspec/sscad-prod.dashhost",
            Document = new HostDocument(
                "sscad_prod",
                "catalogs/sscad-prod.dashcatalog",
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                [
                    new HostLinkDefinition(
                        "agent_admin",
                        "Админка агента",
                        "http://localhost:5280/admin/",
                        "_blank",
                        true,
                        true),
                ],
                [],
                null),
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
        Assert.True(link.Topbar);
        Assert.True(link.Settings);
    }
}
