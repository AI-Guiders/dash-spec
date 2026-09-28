using DashSpec.Abstractions.Viz;
using DashSpec.Plugin.Viz.Builtins.Components.GanttTimeline;

namespace DashSpec.Plugin.Viz.Builtins.Plugins;

public sealed class GanttTimelineVizDashSpecPlugin : VizDashSpecPluginBase
{
    public override string Id => "viz_gantt_timeline";

    public override string DisplayName => "Gantt timeline renderer";

    protected override string RendererId => VizPluginIds.GanttTimeline;

    protected override string RendererDisplayName => "Gantt (timeline)";

    protected override Type CardVizComponentType => typeof(GanttTimelineCardViz);

    protected override IVizPlugin CreateBackend() => new GanttTimelineVizBackend();
}
