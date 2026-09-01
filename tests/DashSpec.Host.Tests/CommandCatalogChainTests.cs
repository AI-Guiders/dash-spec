#nullable enable

using AIGuiders.Platform.CommandPlane;
using DashSpec.Host.Commands;
using DashSpec.Host.Services.Presentation;
using Xunit;

namespace DashSpec.Host.Tests;

/// <summary>Catalog → completion chain (server-side; CCL UI needs interactive render mode on HostCommandBar).</summary>
public sealed class CommandCatalogChainTests
{
    static readonly DashboardCultureAmbient TestCulture =
        new(System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));

    [Fact]
    public void Federation_catalog_loads_surfaces_from_dash_catalog()
    {
        var surfaces = DashboardCatalog.FederationSurfaces;
        Assert.Contains("slash.bar", surfaces);
        Assert.Contains("ccl.filter", surfaces);
    }

    [Fact]
    public void Dashboard_context_builds_filter_and_select_paths()
    {
        var uiState = new DashboardFilterUiState();
        var context = new DashboardFilterContext
        {
            ReportId = "luf",
            ActiveScope = [DashSpecCommandScope.Dashboard],
            ToolbarFilterNames = ["location", "program"],
            FilterIndex = new Dictionary<string, DashSpec.Core.Model.FilterDefinition>(StringComparer.OrdinalIgnoreCase)
            {
                ["location"] = new(
                    DashSpec.Core.Model.FilterKind.Field,
                    "location",
                    null,
                    "location"),
                ["program"] = new(
                    DashSpec.Core.Model.FilterKind.Field,
                    "program",
                    null,
                    "program"),
            },
            UiState = uiState,
            CommandAliases = DashSpec.Core.Model.DashboardDocument.EmptyCommandAliases,
            GetFieldOptions = _ => [],
            Culture = TestCulture.Culture,
        };

        var catalog = DashboardCommandCatalogBuilder.Build(context, []);
        Assert.True(catalog.TryGet(FilterCommandPaths.FilterPath("location"), out _));
        Assert.True(catalog.TryGet(FilterCommandPaths.FilterPath("program"), out _));

        var result = DashboardFilterSlashCompletion.GetResult(catalog, context, "", null, null);
        Assert.NotEmpty(result.Items);
        Assert.Contains(
            result.Items,
            item => string.Equals(item.StepSegment, "select", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.StepSegment, FilterCommandPaths.RootVerb, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Host_catalog_materializes_show_host_phrase_paths()
    {
        var hostContext = HostCommandContextFactory.CreateHostOnly(new DashboardFilterUiState(), TestCulture);
        var catalog = DashboardCommandCatalogBuilder.Build(hostContext, []);

        Assert.Equal("show host dashboard", ShowCommandPaths.SurfacePath("dashboard"));
        Assert.True(catalog.TryGet("show host dashboard", out _));
        Assert.True(catalog.TryGet("show host controlcenter", out _));
    }
}
