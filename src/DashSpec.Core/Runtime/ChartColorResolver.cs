using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Runtime;

public static class ChartColorResolver
{
    private static readonly string[] DefaultPalette =
    [
        "#60a5fa", "#34d399", "#fbbf24", "#f472b6", "#a78bfa", "#fb7185", "#38bdf8", "#4ade80",
    ];

    public static IReadOnlyList<ChartSeries> ApplySeriesColors(
        IReadOnlyList<ChartSeries> series,
        CardDefinition card,
        SpecLibrary? library,
        string? dashboardColorPalette = null)
    {
        var scheme = ResolveScheme(card, library, dashboardColorPalette);
        return series
            .Select(item => item with { Color = scheme.Resolve(item.Name) })
            .ToList();
    }

    internal static IReadOnlyList<string> ResolveLabelColors(
        IReadOnlyList<string> labels,
        CardDefinition card,
        SpecLibrary? library,
        string? dashboardColorPalette = null)
    {
        var scheme = ResolveScheme(card, library, dashboardColorPalette);
        return labels.Select(scheme.Resolve).ToList();
    }

    private static ChartColorScheme ResolveScheme(
        CardDefinition card,
        SpecLibrary? library,
        string? dashboardColorPalette)
    {
        var ordered = new List<string>();
        var bySeries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? defaultColor = null;

        void Absorb(IReadOnlyDictionary<string, string> props)
        {
            if (props.TryGetValue("colors", out var colors) && !string.IsNullOrWhiteSpace(colors))
            {
                ordered.AddRange(ParseColorList(colors));
            }

            if (props.TryGetValue("default", out var def) && !string.IsNullOrWhiteSpace(def))
            {
                defaultColor = def.Trim();
            }

            if (props.TryGetValue("series_colors", out var seriesColors) &&
                !string.IsNullOrWhiteSpace(seriesColors))
            {
                foreach (var (name, color) in ParseSeriesColorMap(seriesColors))
                {
                    bySeries[name] = color;
                }
            }

            if (props.TryGetValue("color_palette", out var paletteName) &&
                !string.IsNullOrWhiteSpace(paletteName))
            {
                ApplyPaletteReference(paletteName, library, ordered, bySeries, ref defaultColor);
            }
        }

        if (!string.IsNullOrWhiteSpace(dashboardColorPalette))
        {
            ApplyPaletteReference(dashboardColorPalette, library, ordered, bySeries, ref defaultColor);
        }

        Absorb(card.Diagram.Properties);

        if (card.Presentation?.UsePreset is { } presentationPreset &&
            library?.TryGetPresentation(presentationPreset) is { } presentationProps)
        {
            Absorb(presentationProps);
        }

        if (card.Presentation is not null)
        {
            Absorb(card.Presentation.Properties);
        }

        if (ordered.Count == 0)
        {
            ordered.AddRange(DefaultPalette);
        }

        return new ChartColorScheme(ordered, bySeries, defaultColor ?? ordered[^1]);
    }

    private static void ApplyPaletteReference(
        string paletteName,
        SpecLibrary? library,
        List<string> ordered,
        Dictionary<string, string> bySeries,
        ref string? defaultColor)
    {
        if (library?.TryGetPalette(paletteName) is { } palette)
        {
            AbsorbPaletteTable(palette, ordered, bySeries, ref defaultColor);
        }
    }

    private static void AbsorbPaletteTable(
        IReadOnlyDictionary<string, string> palette,
        List<string> ordered,
        Dictionary<string, string> bySeries,
        ref string? defaultColor)
    {
        if (palette.TryGetValue("colors", out var colors) && !string.IsNullOrWhiteSpace(colors))
        {
            ordered.Clear();
            ordered.AddRange(ParseColorList(colors));
        }

        if (palette.TryGetValue("default", out var def) && !string.IsNullOrWhiteSpace(def))
        {
            defaultColor = def.Trim();
        }

        foreach (var (key, value) in palette)
        {
            if (IsPaletteMetaKey(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            bySeries[key] = value.Trim();
        }
    }

    private static bool IsPaletteMetaKey(string key) =>
        key.Equals("colors", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("default", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("series_colors", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> ParseColorList(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x));

    private static IEnumerable<(string Name, string Color)> ParseSeriesColorMap(string raw)
    {
        foreach (var segment in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colon = segment.IndexOf(':');
            if (colon <= 0 || colon >= segment.Length - 1)
            {
                continue;
            }

            yield return (segment[..colon].Trim(), segment[(colon + 1)..].Trim());
        }
    }

    private sealed class ChartColorScheme(
        IReadOnlyList<string> ordered,
        IReadOnlyDictionary<string, string> bySeries,
        string defaultColor)
    {
        public string Resolve(string seriesName)
        {
            if (TryResolveMapped(seriesName, out var mapped))
            {
                return mapped;
            }

            if (string.Equals(seriesName, "default", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(seriesName, "Other", StringComparison.OrdinalIgnoreCase))
            {
                return defaultColor;
            }

            return ordered[StablePaletteIndex(seriesName, ordered.Count)];
        }

        private bool TryResolveMapped(string seriesName, out string color)
        {
            if (!string.IsNullOrWhiteSpace(seriesName) &&
                bySeries.TryGetValue(seriesName, out color!))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(seriesName))
            {
                color = defaultColor;
                return false;
            }

            string? bestKey = null;
            foreach (var key in bySeries.Keys)
            {
                if (seriesName.StartsWith(key, StringComparison.OrdinalIgnoreCase) &&
                    (bestKey is null || key.Length > bestKey.Length))
                {
                    bestKey = key;
                }
            }

            if (bestKey is not null)
            {
                color = bySeries[bestKey];
                return true;
            }

            color = defaultColor;
            return false;
        }

        private static int StablePaletteIndex(string seriesName, int paletteLength)
        {
            if (paletteLength <= 0)
            {
                return 0;
            }

            unchecked
            {
                uint hash = 2166136261;
                foreach (var ch in seriesName.Trim().ToUpperInvariant())
                {
                    hash ^= ch;
                    hash *= 16777619;
                }

                return (int)(hash % (uint)paletteLength);
            }
        }
    }
}
