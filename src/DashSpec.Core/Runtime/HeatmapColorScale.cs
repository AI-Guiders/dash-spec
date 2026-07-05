namespace DashSpec.Core.Runtime;

public static class HeatmapColorScale
{
    public static string Normalize(string? scale) =>
        string.IsNullOrWhiteSpace(scale) ? "heat" : scale.Trim().ToLowerInvariant();

    public static string CellBackground(string? scale, double value, double min, double max)
    {
        var normalized = Normalize(scale);
        if (max <= min)
        {
            return normalized switch
            {
                "mono" => "hsl(210, 70%, 45%)",
                _ => "hsl(210, 80%, 48%)",
            };
        }

        var t = (value - min) / (max - min);
        return normalized switch
        {
            "mono" => Mono(t),
            _ => Heat(t),
        };
    }

    public static string CellText(string? scale, double value, double min, double max)
    {
        if (max <= min)
        {
            return "#f8fafc";
        }

        var t = (value - min) / (max - min);
        return t >= 0.45 ? "#0f172a" : "#f8fafc";
    }

    private static string Heat(double t)
    {
        var hue = 215 - t * 215;
        var lightness = 32 + t * 22;
        var saturation = 55 + t * 35;
        return $"hsl({hue:0}, {saturation:0}%, {lightness:0}%)";
    }

    private static string Mono(double t)
    {
        var lightness = 28 + t * 24;
        return $"hsl(210, 65%, {lightness:0}%)";
    }
}
