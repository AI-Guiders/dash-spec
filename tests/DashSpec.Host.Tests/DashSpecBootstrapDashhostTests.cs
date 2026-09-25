using DashSpec.Host.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace DashSpec.Host.Tests;

public sealed class DashSpecBootstrapDashhostTests
{
    [Fact]
    public void ResolveDashhostPath_requires_explicit_reference()
    {
        var root = Path.Combine(Path.GetTempPath(), "dashhost-explicit-" + Guid.NewGuid().ToString("N"));
        var dashspecDir = Path.Combine(root, "dashspec");
        Directory.CreateDirectory(dashspecDir);
        var dashhostPath = Path.Combine(dashspecDir, "sscad-prod.dashhost");
        File.WriteAllText(dashhostPath, "@host x\ncatalog \"c.dashcatalog\"\nend host\n");

        try
        {
            Assert.Null(DashSpecBootstrap.ResolveDashhostPath(root, null));
            Assert.Equal(
                dashhostPath,
                DashSpecBootstrap.ResolveDashhostPath(root, "dashspec/sscad-prod.dashhost"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadBootstrap_rejects_legacy_catalog_path_in_toml()
    {
        var root = Path.Combine(Path.GetTempPath(), "dashhost-legacy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Combine(root, "dash-spec.toml"),
            """
            [host]
            dashhost = "dashspec/demo.dashhost"

            [dashboard]
            catalog_path = "catalogs/demo.dashcatalog"
            """);

        try
        {
            var environment = new TestHostEnvironment { ContentRootPath = root };
            var ex = Assert.Throws<InvalidOperationException>(() => DashSpecBootstrap.LoadBootstrapWithHost(environment));
            Assert.Contains("catalog_path", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadBootstrapWithHost_applies_catalog_from_explicit_dashhost_pointer()
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

        File.WriteAllText(
            Path.Combine(root, "dash-spec.toml"),
            """
            [host]
            dashhost = "dashspec/demo.dashhost"
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
