using DashSpec.Abstractions.Plugins;

namespace DashSpec.Plugin.Viz.Builtins.Plugins;

public static class VizBuiltinsPluginCatalog
{
    public static IReadOnlyList<IDashSpecPlugin> CreateAll() =>
    [
        new ChartJsVizDashSpecPlugin(),
        new MatrixCanvasVizDashSpecPlugin(),
        new CssGridVizDashSpecPlugin(),
        new TableHtmlVizDashSpecPlugin(),
        new ScalarHtmlVizDashSpecPlugin(),
        new GanttHtmlVizDashSpecPlugin(),
        new GanttTimelineVizDashSpecPlugin(),
    ];
}
