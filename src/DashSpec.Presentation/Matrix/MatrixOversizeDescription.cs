namespace DashSpec.Presentation.Matrix;

/// <summary>UI-текст описания превышения лимитов матрицы (ADR-0048: русский текст — presentation, не execution).</summary>
public static class MatrixOversizeDescription
{
    public static string Describe(DashSpec.Modeling.Parse.Card.MatrixRenderLimitsDefinition limits, int xCount, int yCount)
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
