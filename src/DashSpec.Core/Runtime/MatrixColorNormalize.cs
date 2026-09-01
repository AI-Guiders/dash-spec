namespace DashSpec.Core.Runtime;

/// <summary>How heatmap cell colors are normalized to min/max.</summary>
public enum MatrixColorNormalize
{
    /// <summary>Single min/max across the whole matrix.</summary>
    Map,

    /// <summary>Per Y row (line / product / user).</summary>
    Row,

    /// <summary>Per X column (hour / date / bucket).</summary>
    Column,
}

public static class MatrixColorNormalizeParser
{
    public static MatrixColorNormalize Parse(string? raw, MatrixColorNormalize fallback = MatrixColorNormalize.Row) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            null or "" => fallback,
            "map" or "global" or "matrix" or "all" => MatrixColorNormalize.Map,
            "row" or "line" or "y" or "product" => MatrixColorNormalize.Row,
            "column" or "col" or "x" or "hour" => MatrixColorNormalize.Column,
            _ => throw new ArgumentException(
                $"Unknown heatmap color_normalize '{raw}'. Use map, row, or column."),
        };

    public static string ToWire(MatrixColorNormalize mode) => mode switch
    {
        MatrixColorNormalize.Map => "map",
        MatrixColorNormalize.Row => "row",
        MatrixColorNormalize.Column => "column",
        _ => "row",
    };
}
