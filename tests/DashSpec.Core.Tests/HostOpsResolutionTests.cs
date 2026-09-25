using DashSpec.Core.Resolution;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class HostOpsResolutionTests
{
    [Fact]
    public void Presentation_setting_witdb_wins_over_dashhost()
    {
        var resolved = HostOpsResolution.ResolvePresentationSetting(
            "fallback",
            "dashhost",
            "witdb");
        Assert.Equal("witdb", resolved);
    }

    [Fact]
    public void Catalog_path_dashhost_wins_over_toml()
    {
        var resolved = HostOpsResolution.ResolveCatalogPath(
            "fallback",
            "dashhost/catalog.dashcatalog",
            "toml/catalog.dashcatalog");
        Assert.Equal("dashhost/catalog.dashcatalog", resolved);
    }

    [Fact]
    public void Catalog_path_falls_back_to_toml_when_dashhost_missing()
    {
        var resolved = HostOpsResolution.ResolveCatalogPath(
            "fallback",
            null,
            "toml/catalog.dashcatalog");
        Assert.Equal("toml/catalog.dashcatalog", resolved);
    }
}
