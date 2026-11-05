namespace DashSpec.Execution.Runtime;

/// <summary>
/// Slot vertical stretch contract (WPF vocabulary: <c>Stretch</c>/<c>StretchDirection</c>).
/// <c>stretch_mode = fill</c> — the diagram takes the whole slot height instead of a fixed
/// pixel height; <c>none</c> keeps today's fixed-height behaviour.
/// </summary>
public enum VizStretchMode
{
    None,
    Fill,
}

/// <summary>
/// <c>up_only</c> (default, like WPF Viewbox): grow to the slot, never shrink below the
/// natural px height. <c>down_only</c> / <c>both</c> — the mirrored combinations.
/// </summary>
public enum VizStretchDirection
{
    UpOnly,
    DownOnly,
    Both,
}

public static class VizStretch
{
    public static VizStretchMode ResolveMode(IReadOnlyDictionary<string, string> properties) =>
        properties.TryGetValue("stretch_mode", out var raw) &&
        string.Equals(raw.Trim(), "fill", StringComparison.OrdinalIgnoreCase)
            ? VizStretchMode.Fill
            : VizStretchMode.None;

    public static VizStretchDirection ResolveDirection(IReadOnlyDictionary<string, string> properties)
    {
        if (!properties.TryGetValue("stretch_direction", out var raw))
        {
            return VizStretchDirection.UpOnly;
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "down_only" or "downonly" => VizStretchDirection.DownOnly,
            "both" => VizStretchDirection.Both,
            _ => VizStretchDirection.UpOnly,
        };
    }

    /// <summary>Container style for a fill slot; the natural px height rides as the floor/cap.</summary>
    public static string ContainerStyle(VizStretchMode mode, VizStretchDirection direction, int naturalHeightPx) =>
        mode is not VizStretchMode.Fill
            ? string.Empty
            : direction switch
            {
                VizStretchDirection.UpOnly => $"height:100%;min-height:{naturalHeightPx}px;",
                VizStretchDirection.DownOnly => $"height:100%;max-height:{naturalHeightPx}px;",
                _ => "height:100%;",
            };
}
