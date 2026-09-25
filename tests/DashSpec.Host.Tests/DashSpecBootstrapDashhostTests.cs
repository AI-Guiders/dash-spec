using DashSpec.Host.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DashSpec.Host.Tests;

public sealed class DashSpecBootstrapDashhostTests
{
    [Fact]
    public void ResolveDashhostPath_finds_single_dashspec_host_by_convention()
    {
        var root = Path.Combine(Path.GetTempPath(), "dashhost-conv-" + Guid.NewGuid().ToString("N"));
        var dashspecDir = Path.Combine(root, "dashspec");
        Directory.CreateDirectory(dashspecDir);
        var dashhostPath = Path.Combine(dashspecDir, "sscad-prod.dashhost");
        File.WriteAllText(
            dashhostPath,
            """
            @host sscad_prod
            catalog "catalogs/sscad-prod.dashcatalog"
            end host
            """);

        try
        {
            var resolved = DashSpecBootstrap.ResolveDashhostPath(root, null);
            Assert.Equal(dashhostPath, resolved);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadBootstrapWithHost_applies_catalog_from_dashhost_without_toml_catalog_path()
    {
        var root = Path.Combine(Path.GetTempPath(), "dashhost-boot-" + Guid.NewGuid().ToString("N"));
        var dashspecDir = Path.Combine(root, "dashspec");
        var catalogsDir = Path.Combine(dashspecDir, "catalogs");
        Directory.CreateDirectory(catalogsDir);

        File.WriteAllText(
            Path.Combine(catalogsDir, "demo.dashcatalog"),
            """
            @catalog demo
            entry overview dashspec "overview.dashspec"
            """);

        File.WriteAllText(
            Path.Combine(dashspecDir, "demo.dashhost"),
            """
            @host demo
            catalog "catalogs/demo.dashcatalog"
            end host
            """);

        try
        {
            var environment = new TestHostEnvironment { ContentRootPath = root };
            var (_, hostShell) = DashSpecBootstrap.LoadBootstrapWithHost(environment);
            Assert.NotNull(hostShell);
            Assert.EndsWith("demo.dashcatalog", hostShell!.Document.CatalogPath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "DashSpec.Host.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
