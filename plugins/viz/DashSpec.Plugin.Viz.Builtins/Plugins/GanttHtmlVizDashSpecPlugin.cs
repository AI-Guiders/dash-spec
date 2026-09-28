using DashSpec.Abstractions.Viz;
using DashSpec.Plugin.Viz.Builtins.Components.GanttHtml;

namespace DashSpec.Plugin.Viz.Builtins.Plugins;

public sealed class GanttHtmlVizDashSpecPlugin : VizDashSpecPluginBase
{
    public override string Id => "viz_gantt_html";

    public override string DisplayName => "Gantt compact renderer";

    protected override string RendererId => VizPluginIds.GanttHtml;

    protected override string RendererDisplayName => "Gantt (compact)";

    protected override Type CardVizComponentType => typeof(GanttCardViz);

    protected override IVizPlugin CreateBackend() => new GanttHtmlVizBackend();
}
