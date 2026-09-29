namespace DashSpec.Viz;

public enum VizDisplayToggleKind
{
    ValueLabels,
    AxisLabelsX,
    AxisLabelsY,
    Legend,
}

public static class VizCardDisplayActions
{
    public const string ToggleValueLabels = "toggle_viz_value_labels";
    public const string ToggleAxisLabelsX = "toggle_viz_axis_labels_x";
    public const string ToggleAxisLabelsY = "toggle_viz_axis_labels_y";
    public const string ToggleLegend = "toggle_viz_legend";

    public const string LegacyToggleValueLabels = "toggle_matrix_value_labels";
    public const string LegacyToggleAxisLabelsX = "toggle_matrix_axis_labels_x";
    public const string LegacyToggleAxisLabelsY = "toggle_matrix_axis_labels_y";

    public static bool TryMap(string actionId, out VizDisplayToggleKind kind)
    {
        switch (actionId.Trim().ToLowerInvariant())
        {
            case ToggleValueLabels:
            case LegacyToggleValueLabels:
                kind = VizDisplayToggleKind.ValueLabels;
                return true;
            case ToggleAxisLabelsX:
            case LegacyToggleAxisLabelsX:
                kind = VizDisplayToggleKind.AxisLabelsX;
                return true;
            case ToggleAxisLabelsY:
            case LegacyToggleAxisLabelsY:
                kind = VizDisplayToggleKind.AxisLabelsY;
                return true;
            case ToggleLegend:
                kind = VizDisplayToggleKind.Legend;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    public static string CanonicalActionId(VizDisplayToggleKind kind) =>
        kind switch
        {
            VizDisplayToggleKind.ValueLabels => ToggleValueLabels,
            VizDisplayToggleKind.AxisLabelsX => ToggleAxisLabelsX,
            VizDisplayToggleKind.AxisLabelsY => ToggleAxisLabelsY,
            VizDisplayToggleKind.Legend => ToggleLegend,
            _ => ToggleValueLabels,
        };
}
