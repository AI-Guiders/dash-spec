using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class HostModuleParserTests
{
    static HostModuleParserTests()
    {
        DashSpecParser.EnsureModuleParsersRegistered();
    }

    [Fact]
    public void Parse_host_with_catalog_presentation_and_links()
    {
        const string text = """
            @host sscad_prod

            catalog "catalogs/sscad-prod.dashcatalog"

            configuration
              language = ru
              display_timezone = "Europe/Moscow"
            end configuration

            presentation
              product_title = "License Usage"
              catalog_label = "Отчёт"
              color_scheme = dark
            end presentation

            links
              link portal as "Портал"
                url = "https://example/sscad"
                topbar = true
              end link
            end links

            surfaces
              show help, settings
            end surfaces

            end host
            """;

        var host = HostModuleParser.Parse(text, @"D:\planet\dashspec");

        Assert.Equal("sscad_prod", host.Id);
        Assert.Equal("catalogs/sscad-prod.dashcatalog", host.CatalogPath);
        Assert.Equal("ru", host.Configuration["language"]);
        Assert.Equal("License Usage", host.Presentation["product_title"]);
        Assert.Equal("Отчёт", host.Presentation["catalog_label"]);
        Assert.Single(host.Links);
        Assert.Equal("Портал", host.Links[0].Label);
        Assert.Contains("help", host.Surfaces);
        Assert.Contains("settings", host.Surfaces);
    }

    [Fact]
    public void Parse_host_with_bang_include_layout()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashhost-include-" + Guid.NewGuid().ToString("N"));
        var layoutsDir = Path.Combine(dir, "layouts");
        Directory.CreateDirectory(layoutsDir);
        File.WriteAllText(
            Path.Combine(layoutsDir, "host-topbar.dashlayout"),
            """
            @layout tb
            scope host
            [ catalog nav ]
            """);

        const string text = """
            @host demo
            catalog "catalogs/demo.dashcatalog"
            !include "layouts/host-topbar.dashlayout"
            end host
            """;

        try
        {
            var host = HostModuleParser.Parse(text, dir);
            Assert.NotNull(host.TopbarLayout);
            Assert.Equal(LayoutScope.Host, host.TopbarLayout!.ModuleScope);
            Assert.Equal(["catalog", "nav"], host.TopbarLayout!.Rows[0]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parse_host_rejects_missing_catalog()
    {
        const string text = """
            @host empty
            end host
            """;

        var ex = Assert.Throws<DashSpecParseException>(() => HostModuleParser.Parse(text, null));
        Assert.Contains("catalog", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
