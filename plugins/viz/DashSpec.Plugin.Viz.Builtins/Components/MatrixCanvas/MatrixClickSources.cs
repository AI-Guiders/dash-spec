namespace DashSpec.Plugin.Viz.Builtins.Components.MatrixCanvas;

/// <summary>
/// Matrix click sources (ADR-0107): the source vocabulary this plugin renders.
/// The parse-time registry (KnownClickSources) is fed from these words.
/// </summary>
public static class MatrixClickSources
{
    /// <summary>Heatmap cell click (the X×Y cell body).</summary>
    public const string Cell = "cell";

    /// <summary>Axis category item click (a Y-axis row label).</summary>
    public const string CategoryItem = "category item";
}
