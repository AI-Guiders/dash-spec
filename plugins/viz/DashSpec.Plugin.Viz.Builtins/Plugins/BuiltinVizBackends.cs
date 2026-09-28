using DashSpec.Abstractions.Viz;

namespace DashSpec.Plugin.Viz.Builtins.Plugins;

public sealed class ChartJsVizBackend : IVizPlugin
{
    public string Id => VizPluginIds.ChartJs;

    public string DataFamily => "chart";
}

public sealed class MatrixCanvasVizBackend : IVizPlugin
{
    public string Id => VizPluginIds.MatrixCanvas;

    public string DataFamily => "matrix";
}

public sealed class CssGridVizBackend : IVizPlugin
{
    public string Id => VizPluginIds.CssGrid;

    public string DataFamily => "matrix";
}

public sealed class TableHtmlVizBackend : IVizPlugin
{
    public string Id => VizPluginIds.TableHtml;

    public string DataFamily => "table";
}

public sealed class ScalarHtmlVizBackend : IVizPlugin
{
    public string Id => VizPluginIds.ScalarHtml;

    public string DataFamily => "scalar";
}

public sealed class GanttHtmlVizBackend : IVizPlugin
{
    public string Id => VizPluginIds.GanttHtml;

    public string DataFamily => "gantt";
}

public sealed class GanttTimelineVizBackend : IVizPlugin
{
    public string Id => VizPluginIds.GanttTimeline;

    public string DataFamily => "gantt";
}
