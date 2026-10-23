using DashSpec.Abstractions.Viz;

namespace DashSpec.Stack.Radzen;

/// <summary>
/// Radzen render stack (ADR-0112): the surface speaks Radzen — chart, table, scalar and
/// gantt families render through the Radzen plugin; what Radzen cannot express well is
/// delegated to a guest renderer (matrix → matrix-canvas). Heatmap is native: the plugin's
/// square heatmap draws it (matrix payload + drill-down).
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
            ["Scalar"] = RendererId,
            ["Gantt"] = RendererId,
            ["Matrix"] = VizPluginIds.MatrixCanvas, // guest: no Radzen matrix canvas
        };

    public IReadOnlyDictionary<string, string> KindPlugins { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // The Matrix family is the canvas guest, but kind heatmap is ours: the square
            // heatmap draws the matrix payload (ADR-0112 §1 — the stack routes kinds).
            ["heatmap"] = RendererId,
        };
}
