using DashSpec.Abstractions.Viz;

namespace DashSpec.Host.Plugins.Builtins;

public sealed class ChartJsVizPlugin : IVizPlugin
{
    public string Id => VizPluginIds.ChartJs;

    public string DataFamily => "chart";
}

public sealed class MatrixCanvasVizPlugin : IVizPlugin
{
    public string Id => VizPluginIds.MatrixCanvas;

    public string DataFamily => "matrix";
}

public sealed class CssGridVizPlugin : IVizPlugin
{
    public string Id => VizPluginIds.CssGrid;

    public string DataFamily => "matrix";
}

public sealed class TableHtmlVizPlugin : IVizPlugin
{
    public string Id => VizPluginIds.TableHtml;

    public string DataFamily => "table";
}

public sealed class ScalarHtmlVizPlugin : IVizPlugin
{
    public string Id => VizPluginIds.ScalarHtml;

    public string DataFamily => "scalar";
}
