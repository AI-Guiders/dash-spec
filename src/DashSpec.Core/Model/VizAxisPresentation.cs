namespace DashSpec.Core.Model;

public enum VizValueLabelMode
{
    Auto,
    Show,
    Hide,
}

/// <summary>Diagram-level axis/value label presentation (ADR-0062).</summary>
public interface IVizAxisPresentationSpec
{
    VizValueLabelMode ValueLabels { get; }

    bool AxisLabelsX { get; }

    bool AxisLabelsY { get; }
}

public static class VizAxisPresentationParser
{
    public const int DefaultValueLabelsThresholdPx = 14;

    public static VizValueLabelMode ParseValueLabels(
        IReadOnlyDictionary<string, string> properties,
        VizValueLabelMode defaultMode = VizValueLabelMode.Auto)
    {
        if (!properties.TryGetValue("value_labels", out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return defaultMode;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "show" => VizValueLabelMode.Show,
            "hide" => VizValueLabelMode.Hide,
            "auto" => VizValueLabelMode.Auto,
            _ => defaultMode,
        };
    }

    public static bool ParseAxisLabels(
        IReadOnlyDictionary<string, string> properties,
        string propertyName,
        bool defaultShow = true)
    {
        if (!properties.TryGetValue(propertyName, out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return defaultShow;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "hide" => false,
            "show" => true,
            _ => defaultShow,
        };
    }

    public static int ParseValueLabelsThreshold(
        IReadOnlyDictionary<string, string> properties,
        int defaultThresholdPx = DefaultValueLabelsThresholdPx)
    {
        if (!properties.TryGetValue("value_labels_threshold", out var raw) ||
            string.IsNullOrWhiteSpace(raw) ||
            !int.TryParse(raw.Trim(), out var parsed))
        {
            return defaultThresholdPx;
        }

        return Math.Clamp(parsed, 6, 96);
    }

    public static string? ParseToolbarLabel(IReadOnlyDictionary<string, string> properties, string propertyName)
    {
        if (!properties.TryGetValue(propertyName, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return raw.Trim();
    }
}

public static class VizLabelDisplayResolver
{
    public static string ResolveValueLabelsWire(VizValueLabelMode specMode, bool? userOverride)
    {
        if (userOverride is bool visible)
        {
            return visible ? "show" : "hide";
        }

        return specMode switch
        {
            VizValueLabelMode.Show => "show",
            VizValueLabelMode.Hide => "hide",
            _ => "auto",
        };
    }

    public static bool ResolveAxisVisible(bool specShow, bool? userOverride) =>
        userOverride ?? specShow;

    public static bool EffectiveValueLabelsVisible(VizValueLabelMode specMode, bool? userOverride)
    {
        if (userOverride is bool visible)
        {
            return visible;
        }

        return specMode != VizValueLabelMode.Hide;
    }

    public static bool EffectiveAxisVisible(bool specShow, bool? userOverride) =>
        ResolveAxisVisible(specShow, userOverride);
}
