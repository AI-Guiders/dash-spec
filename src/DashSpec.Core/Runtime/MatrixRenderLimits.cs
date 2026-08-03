using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

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
}
