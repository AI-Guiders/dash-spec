using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Core.Runtime;

internal static class ChartChromeProperties
{
    public static Dictionary<string, string> Merge(CardDefinition card, SpecLibrary? library)
    {
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AbsorbPresentationChain(card.Presentation, library, merged);

        foreach (var legacyKey in new[]
        {
            "legend", "height", "stacked", "orientation", "fill",
            "scale_value", "scale_measure", "scale_x", "scale_y", "value_scale", "y_format",
            "y_max", "value_axis_max", "color_mode", "default", "colors",
        })
        {
            if (!merged.ContainsKey(legacyKey) &&
                card.Diagram.Properties.TryGetValue(legacyKey, out var legacyValue))
            {
                merged[legacyKey] = legacyValue;
            }
        }

        return merged;
    }

    public static bool TryGetPercentCap(
        CardDefinition card,
        SpecLibrary? library,
        out double cap)
    {
        cap = 0;
        var merged = Merge(card, library);
        return TryReadAxisMax(merged, "y_max", out cap) ||
               TryReadAxisMax(merged, "value_axis_max", out cap);
    }

    private static void AbsorbPresentationChain(
        PresentationBlock? block,
        SpecLibrary? library,
        Dictionary<string, string> merged)
    {
        if (block is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(block.UsePreset))
        {
            AbsorbPresentationPreset(block.UsePreset, library, merged, visited: []);
        }

        foreach (var (key, value) in block.Properties)
        {
            if (!string.Equals(key, "use", StringComparison.OrdinalIgnoreCase))
            {
                merged[key] = value;
            }
        }
    }

    private static void AbsorbPresentationPreset(
        string presetName,
        SpecLibrary? library,
        Dictionary<string, string> merged,
        HashSet<string> visited)
    {
        if (!visited.Add(presetName))
        {
            return;
        }

        if (library?.TryGetPresentation(presetName) is not { } preset)
        {
            return;
        }

        foreach (var (key, value) in preset)
        {
            merged[key] = value;
        }
    }

    private static bool TryReadAxisMax(
        IReadOnlyDictionary<string, string> properties,
        string key,
        out double value)
    {
        value = 0;
        return properties.TryGetValue(key, out var raw) &&
               double.TryParse(
                   raw,
                   System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out value) &&
               value > 0;
    }
}
