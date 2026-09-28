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

    protected override IVizPlugin CreateBackend() => new ChartJsVizBackend();
}
