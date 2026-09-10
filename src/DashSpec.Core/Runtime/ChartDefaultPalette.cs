using System.Globalization;

namespace DashSpec.Core.Runtime;

/// <summary>256 distinct hex colors for series/category charts (remark 17). Hex required — Host JS adds alpha suffix.</summary>
internal static class ChartDefaultPalette
{
    public const int Count = 256;

    public static IReadOnlyList<string> Values { get; } = Build();

    private static string[] Build()
    {
        var palette = new string[Count];
        ReadOnlySpan<string> seed =
        [
            "#60a5fa", "#34d399", "#fbbf24", "#f472b6", "#a78bfa", "#fb7185", "#38bdf8", "#4ade80",
            "#f97316", "#14b8a6", "#e879f9", "#84cc16", "#0ea5e9", "#ef4444", "#6366f1", "#eab308",
        ];

        for (var i = 0; i < seed.Length; i++)
        {
            palette[i] = seed[i];
        }

        for (var i = seed.Length; i < Count; i++)
        {
            palette[i] = GenerateHex(i);
        }

        return palette;
    }

    /// <summary>32 hue bands × 8 tone steps → 256 slots (indices 16+ after seed).</summary>
    private static string GenerateHex(int index)
    {
        var slot = index - 16;
        var hueBand = slot % 32;
        var tone = slot / 32;
        var hue = hueBand * (360.0 / 32.0) + tone * 0.85;
        var saturation = 52 + (tone % 4) * 7;
        var lightness = 41 + (tone % 8) * 3;
        return HslToHex(hue, saturation, lightness);
    }

    private static string HslToHex(double hue, double saturation, double lightness)
    {
        hue = ((hue % 360) + 360) % 360;
        var s = Math.Clamp(saturation, 0, 100) / 100.0;
        var l = Math.Clamp(lightness, 0, 100) / 100.0;
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(hue / 60 % 2 - 1));
        var m = l - c / 2;

        double r;
        double g;
        double b;
        if (hue < 60)
        {
            r = c; g = x; b = 0;
        }
        else if (hue < 120)
        {
            r = x; g = c; b = 0;
        }
        else if (hue < 180)
        {
            r = 0; g = c; b = x;
        }
        else if (hue < 240)
        {
            r = 0; g = x; b = c;
        }
        else if (hue < 300)
        {
            r = x; g = 0; b = c;
        }
        else
        {
            r = c; g = 0; b = x;
        }

        return string.Create(CultureInfo.InvariantCulture, $"#{ToByte(r + m):x2}{ToByte(g + m):x2}{ToByte(b + m):x2}");
    }

    private static int ToByte(double channel) =>
        (int)Math.Round(Math.Clamp(channel, 0, 1) * 255);
}
