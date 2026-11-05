namespace DashSpec.Abstractions.Viz;

/// <summary>
/// Matrix click sources (ADR-0107): the source vocabulary shared by every matrix renderer
/// (builtin canvas heatmap, Radzen square heatmap). The parse-time registry
/// (KnownClickSources) is fed from these words — renderers must not invent private
/// vocabularies for the same payloads.
/// </summary>
public static class MatrixClickSources
{
    /// <summary>Heatmap cell click (the X×Y cell body).</summary>
    public const string Cell = "cell";

    /// <summary>Axis category item click (a Y-axis row label).</summary>
    public const string CategoryItem = "category item";
}
