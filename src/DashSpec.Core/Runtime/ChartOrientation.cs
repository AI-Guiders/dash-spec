namespace DashSpec.Core.Runtime;

public enum ChartOrientation
{
    Vertical,
    Horizontal,
}

public static class ChartOrientationParser
{
    public static ChartOrientation Parse(string? raw, ChartOrientation fallback = ChartOrientation.Vertical) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "horizontal" or "h" or "barh" => ChartOrientation.Horizontal,
            "vertical" or "v" or "barv" or "" => ChartOrientation.Vertical,
            null => fallback,
            _ => fallback,
        };
}
