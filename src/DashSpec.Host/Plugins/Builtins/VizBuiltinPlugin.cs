using DashSpec.Abstractions.Plugins;
using DashSpec.Abstractions.Viz;
using DashSpec.Host.Components.Dashboard.Viz;
using DashSpec.Host.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DashSpec.Host.Plugins.Builtins;

public sealed class VizBuiltinPlugin : IDashSpecPlugin
{
    public string Id => "viz_builtin";

    public string DisplayName => "Built-in viz renderers";

    public PluginTier Tier => PluginTier.Core;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IVizPlugin, ChartJsVizPlugin>();
        services.AddSingleton<IVizPlugin, CssGridVizPlugin>();
        services.AddSingleton<IVizPlugin, MatrixCanvasVizPlugin>();
        services.AddSingleton<IVizPlugin, TableHtmlVizPlugin>();
        services.AddSingleton<IVizPlugin, ScalarHtmlVizPlugin>();

        services.AddSingleton(sp =>
        {
            var registry = new CardVizComponentRegistry();
            registry.Register(VizPluginIds.ChartJs, typeof(ChartJsCardViz));
            registry.Register(VizPluginIds.TableHtml, typeof(TableHtmlCardViz));
            registry.Register(VizPluginIds.ScalarHtml, typeof(ScalarHtmlCardViz));
            registry.Register(VizPluginIds.CssGrid, typeof(CssGridCardViz));
            registry.Register(VizPluginIds.MatrixCanvas, typeof(MatrixCanvasCardViz));
            return registry;
        });
    }

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        registry.AddVizRenderer(new VizRendererDescriptor(Id, VizPluginIds.ChartJs, "Chart"));
        registry.AddVizRenderer(new VizRendererDescriptor(Id, VizPluginIds.CssGrid, "Matrix (CSS grid, legacy)"));
        registry.AddVizRenderer(new VizRendererDescriptor(Id, VizPluginIds.MatrixCanvas, "Matrix (canvas)"));
        registry.AddVizRenderer(new VizRendererDescriptor(Id, VizPluginIds.TableHtml, "Table"));
        registry.AddVizRenderer(new VizRendererDescriptor(Id, VizPluginIds.ScalarHtml, "Scalar"));
    }
}
