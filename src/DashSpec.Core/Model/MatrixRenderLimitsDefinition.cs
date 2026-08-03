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
}
