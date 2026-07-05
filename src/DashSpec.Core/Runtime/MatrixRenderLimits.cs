namespace DashSpec.Core.Runtime;

public static class MatrixRenderLimits
{
    public const int MaxCells = 2500;

    public const int MaxAxisLabels = 80;

    public static bool IsOversized(int xCount, int yCount) =>
        xCount * yCount > MaxCells ||
        xCount > MaxAxisLabels ||
        yCount > MaxAxisLabels;
}
