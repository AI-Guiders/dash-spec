using DashSpec.Abstractions.Viz;

namespace DashSpec.Stack.SelfMade;

/// <summary>
/// SelfMade render stack (ADR-0112): the pre-Radzen visual language — every family is
/// expressed by the builtin renderers (chartjs, table-html, scalar-html, matrix-canvas,
/// gantt). Exactly how it looked before Radzen.
/// </summary>
public sealed class SelfMadeRenderStack : IRenderStack
{
    public string Name => "selfmade";

    public IReadOnlyDictionary<string, string> FamilyPlugins { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Chart"] = VizPluginIds.ChartJs,
            ["Table"] = VizPluginIds.TableHtml,
            ["Scalar"] = VizPluginIds.ScalarHtml,
            ["Matrix"] = VizPluginIds.MatrixCanvas,
            ["Gantt"] = VizPluginIds.GanttHtml,
        };

    public IReadOnlyDictionary<string, string> KindPlugins { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
