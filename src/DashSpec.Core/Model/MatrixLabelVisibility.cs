namespace DashSpec.Core.Model;

public enum MatrixValueLabelMode
{
    Auto,
    Show,
    Hide,
}

public static class MatrixLabelVisibilityParser
{
    public const int DefaultValueLabelsThresholdPx = 14;

    public static MatrixValueLabelMode ParseValueLabels(
        IReadOnlyDictionary<string, string> properties,
        MatrixValueLabelMode defaultMode = MatrixValueLabelMode.Auto)
    {
        if (!properties.TryGetValue("value_labels", out var raw) ||
            string.IsNullOrWhiteSpace(raw))
        {
            return defaultMode;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "show" => MatrixValueLabelMode.Show,
            "hide" => MatrixValueLabelMode.Hide,
            "auto" => MatrixValueLabelMode.Auto,
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

    /// <summary>Minimum cell width and height (px) to draw values in <c>value_labels = auto</c>.</summary>
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
}

public static class MatrixLabelDisplayResolver
{
    public static string ResolveValueLabelsWire(MatrixValueLabelMode specMode, bool? userOverride)
    {
        if (userOverride is bool visible)
        {
            return visible ? "show" : "hide";
        }

        return specMode switch
        {
            MatrixValueLabelMode.Show => "show",
            MatrixValueLabelMode.Hide => "hide",
            _ => "auto",
        };
    }

    public static bool ResolveAxisVisible(bool specShow, bool? userOverride) =>
        userOverride ?? specShow;

    public static bool EffectiveValueLabelsVisible(MatrixValueLabelMode specMode, bool? userOverride)
    {
        if (userOverride is bool visible)
        {
            return visible;
        }

        return specMode != MatrixValueLabelMode.Hide;
    }

    public static bool EffectiveAxisVisible(bool specShow, bool? userOverride) =>
        ResolveAxisVisible(specShow, userOverride);
}
