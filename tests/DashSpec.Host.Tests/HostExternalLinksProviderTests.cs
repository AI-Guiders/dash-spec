using DashSpec.Host.Configuration;
using DashSpec.Host.Services.Presentation;
using Xunit;

namespace DashSpec.Host.Tests;

public sealed class HostExternalLinksProviderTests
{
    [Fact]
    public void Load_links_from_runtime_toml()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-links-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var runtimePath = Path.Combine(dir, "runtime.toml");
        File.WriteAllText(
            runtimePath,
            """
            [connectors.sqlserver]
            connection_string = "Server=.;Database=test;Trusted_Connection=True"

            [[plugins.load]]
            id = "sqlserver"
            assembly = "DashSpec.Connector.SqlServer.dll"
            is_connector = true

            [[links]]
            id = "agent_admin"
            label = "Админка агента"
            url = "http://localhost:5280/admin/"
            target = "_blank"
            """);

        try
        {
            var provider = new HostExternalLinksProvider(new DashSpecHostContext
            {
                StartupRuntimeConfigPath = runtimePath,
                StartupRuntimeReference = "runtime.toml",
                DefaultSpecRelativePath = "spec.dashspec",
                DefaultSpecDirectory = dir,
                Catalog = null!,
            });

            var links = provider.ForStartupRuntime();
            var link = Assert.Single(links);
            Assert.Equal("Админка агента", link.Label);
            Assert.Equal("http://localhost:5280/admin/", link.Url);
            Assert.Equal("_blank", link.Target);
            Assert.True(link.Topbar);
            Assert.True(link.Settings);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
