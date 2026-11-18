using DashSpec.Abstractions.Plugins;
using DashSpec.Abstractions.Viz;
using DashSpec.Plugin.Viz.Builtins.Components.ChartJs;

namespace DashSpec.Plugin.Viz.Builtins.Plugins;

public sealed class ChartJsVizDashSpecPlugin : VizDashSpecPluginBase
{
    public override string Id => "viz_chartjs";

    public override string DisplayName => "Chart.js renderer";

    protected override string RendererId => VizPluginIds.ChartJs;

    protected override string RendererDisplayName => "Chart";

    protected override Type CardVizComponentType => typeof(ChartJsCardViz);

    protected override Type? PluginHeadComponentType => typeof(VizBuiltinsHead);

    protected override Type? PluginScriptsComponentType => typeof(VizBuiltinsScripts);

    protected override IVizPlugin CreateBackend() => new ChartJsVizBackend();

    public override void RegisterContributors(IDashSpecContributorRegistry registry)
    {
        base.RegisterContributors(registry);
        registry.RegisterPluginComponent(
            DashSpecPluginComponents.CardDiagram,
            typeof(Components.Hosting.CardVizHost));
    }
}
