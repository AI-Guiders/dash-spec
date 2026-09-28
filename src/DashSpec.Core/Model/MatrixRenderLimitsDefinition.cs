namespace DashSpec.Core.Model;

/// <summary>Per-card matrix/heatmap render guard (ADR-0030). Host defaults when omitted.</summary>
public sealed record MatrixRenderLimitsDefinition(int? MaxCells = null, int? MaxAxisLabels = null)
{
    public static MatrixRenderLimitsDefinition Default { get; } = new();

    public int EffectiveMaxCells => MaxCells ?? 2500;

    public int EffectiveMaxAxisLabels => MaxAxisLabels ?? 80;

    public bool IsOversized(int xCount, int yCount) =>
        xCount * yCount > EffectiveMaxCells ||
        xCount > EffectiveMaxAxisLabels ||
        yCount > EffectiveMaxAxisLabels;

    /// <summary>Human-readable reason when <see cref="IsOversized"/> is true.</summary>
    public string DescribeOversize(int xCount, int yCount)
    {
        if (!IsOversized(xCount, yCount))
        {
            return string.Empty;
        }

        var parts = new List<string>();
        var cells = xCount * yCount;
        if (cells > EffectiveMaxCells)
        {
            parts.Add($"ячеек {xCount}×{yCount}={cells} > {EffectiveMaxCells}");
        }

        if (xCount > EffectiveMaxAxisLabels)
        {
            parts.Add($"столбцов X ({xCount}) > {EffectiveMaxAxisLabels}");
        }

        if (yCount > EffectiveMaxAxisLabels)
        {
            parts.Add($"строк Y ({yCount}) > {EffectiveMaxAxisLabels}");
        }

        return string.Join("; ", parts);
    }
}
