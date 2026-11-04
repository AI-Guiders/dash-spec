using DashSpec.Abstractions.Viz;

namespace DashSpec.Stack.Radzen;

/// <summary>
/// Radzen render stack (ADR-0112): the surface speaks Radzen — chart, table, scalar and
/// gantt families render through the Radzen plugin; what Radzen cannot express well is
/// delegated to a guest renderer (matrix → matrix-canvas; kind heatmap → ECharts — Radzen's
/// responsive sizing fights square cells, and a hand-rolled grid inside the Radzen stack
/// would not be Radzen's visual language anyway).
/// </summary>
public sealed class RadzenRenderStack : IRenderStack
{
    public const string RendererId = "radzen";

    public string Name => "radzen";

    public IReadOnlyDictionary<string, string> FamilyPlugins { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Chart"] = RendererId,
            ["Table"] = RendererId,
            ["Gantt"] = RendererId,
            ["Matrix"] = VizPluginIds.MatrixCanvas, // guest: no Radzen matrix canvas
            ["Scalar"] = RendererId // KPI numbers: RadzenRadialGauge / value tile (RadzenCardViz)
        };

    public IReadOnlyDictionary<string, string> KindPlugins { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    // Heatmap no longer delegates to a guest: the matrix family default (MatrixCanvas,
    // the builtin) renders it — ADR-0111: the renderer paints the tree it is given;
    // no private geometry engine negotiates with CSS at runtime.
}
