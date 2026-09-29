using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

public static class PlacementGridCss
{
    public static string InteriorGridVariables(int columns) =>
        $"--card-grid-columns:{columns};";

    public static string SlotStyle(PlacementDefinition placement, int columns)
    {
        var span = Math.Min(placement.Span, columns);
        return placement.Row > 0
            ? $"grid-column:{placement.Col} / span {span};grid-row:{placement.Row};"
            : $"grid-column:span {span};";
    }
}
