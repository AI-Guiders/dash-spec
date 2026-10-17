using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

public static class MatrixRenderLimits
{
    public const int DefaultMaxCells = 2500;

    public const int DefaultMaxAxisLabels = 80;

    public static bool IsOversized(int xCount, int yCount) =>
        MatrixRenderLimitsDefinition.Default.IsOversized(xCount, yCount);

    public static bool IsOversized(
        int xCount,
        int yCount,
        MatrixRenderLimitsDefinition? limits) =>
        (limits ?? MatrixRenderLimitsDefinition.Default).IsOversized(xCount, yCount);

    public static string DescribeOversize(MatrixRenderLimitsDefinition limits, int xCount, int yCount)
    {
        if (!limits.IsOversized(xCount, yCount))
        {
            return string.Empty;
        }

        var parts = new List<string>();
        var cells = xCount * yCount;
        if (cells > limits.EffectiveMaxCells)
        {
            parts.Add($"ячеек {xCount}×{yCount}={cells} > {limits.EffectiveMaxCells}");
        }

        if (xCount > limits.EffectiveMaxAxisLabels)
        {
            parts.Add($"столбцов X ({xCount}) > {limits.EffectiveMaxAxisLabels}");
        }

        if (yCount > limits.EffectiveMaxAxisLabels)
        {
            parts.Add($"строк Y ({yCount}) > {limits.EffectiveMaxAxisLabels}");
        }

        return string.Join("; ", parts);
    }
}
