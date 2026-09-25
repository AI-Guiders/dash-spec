using DashSpec.Host.Configuration;
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
            var hostDatabase = HostTestServices.CreateHostDatabase();
            var ex = Assert.Throws<InvalidOperationException>(() => DashSpecBootstrap.LoadBootstrapWithHost(environment, hostDatabase));
            Assert.Contains("catalog_path", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadBootstrap_rejects_presentation_in_local_toml()
    {
        var root = Path.Combine(Path.GetTempPath(), "dashhost-pres-" + Guid.NewGuid().ToString("N"));
        var dashspecDir = Path.Combine(root, "dashspec");
        Directory.CreateDirectory(dashspecDir);
        File.WriteAllText(
            Path.Combine(dashspecDir, "demo.dashhost"),
            "@host x\ncatalog \"c.dashcatalog\"\nend host\n");
        File.WriteAllText(
            Path.Combine(root, "dash-spec.toml"),
            """
            [host]
            dashhost = "dashspec/demo.dashhost"
            """);
        File.WriteAllText(
            Path.Combine(root, "dash-spec.local.toml"),
            """
            [presentation]
            language = "en"
            """);

        try
        {
            var environment = new TestHostEnvironment { ContentRootPath = root };
            var hostDatabase = HostTestServices.CreateHostDatabase();
            var ex = Assert.Throws<InvalidOperationException>(() => DashSpecBootstrap.LoadBootstrapWithHost(environment, hostDatabase));
            Assert.Contains("presentation", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadBootstrap_rejects_links_in_ops_toml()
    {
        var root = Path.Combine(Path.GetTempPath(), "dashhost-links-" + Guid.NewGuid().ToString("N"));
        var dashspecDir = Path.Combine(root, "dashspec");
        Directory.CreateDirectory(dashspecDir);
        File.WriteAllText(
            Path.Combine(dashspecDir, "demo.dashhost"),
            "@host x\ncatalog \"c.dashcatalog\"\nend host\n");
        File.WriteAllText(
            Path.Combine(root, "dash-spec.toml"),
            """
            [host]
            dashhost = "dashspec/demo.dashhost"

            [[links]]
            id = "docs"
            label = "Docs"
            url = "https://example.com"
            """);

        try
        {
            var environment = new TestHostEnvironment { ContentRootPath = root };
            var hostDatabase = HostTestServices.CreateHostDatabase();
            var ex = Assert.Throws<InvalidOperationException>(() => DashSpecBootstrap.LoadBootstrapWithHost(environment, hostDatabase));
            Assert.Contains("links", ex.Message, StringComparison.OrdinalIgnoreCase);
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
            var hostDatabase = HostTestServices.CreateHostDatabase();
            var (_, hostShell) = DashSpecBootstrap.LoadBootstrapWithHost(environment, hostDatabase);
            Assert.NotNull(hostShell);
            Assert.EndsWith("demo.dashcatalog", hostShell!.Document.CatalogPath, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

}
