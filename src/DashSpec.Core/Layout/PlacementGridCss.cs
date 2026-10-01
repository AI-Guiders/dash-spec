using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

public static class PlacementGridCss
{
    public static string HostGridVariables(LayoutDefinition layout) =>
        $"--grid-columns:{layout.Columns};--grid-gap:{layout.GapPx}px;";

    public static string InteriorGridVariables(int columns) =>
        $"--card-grid-columns:{columns};";

    public static string GridVariables(LayoutSlotScope scope, LayoutDefinition layout) =>
        scope switch
        {
            LayoutSlotScope.HostTabBoard or LayoutSlotScope.HostPageToolbar => HostGridVariables(layout),
            _ => InteriorGridVariables(layout.Columns),
        };

    /// <summary>Card filter chrome row: apply slot column shrinks to icon (ADR-0061).</summary>
    public static string ChromeGridStyle(
        IReadOnlyDictionary<string, PlacementDefinition> placements,
        int columnCount)
    {
        if (columnCount <= 0 || placements.Count == 0)
        {
            return InteriorGridVariables(Math.Max(columnCount, 1));
        }

        var tracks = new string[columnCount];
        for (var i = 0; i < columnCount; i++)
        {
            tracks[i] = "minmax(0,1fr)";
        }

        foreach (var (slotId, placement) in placements)
        {
            if (!string.Equals(slotId, CardLocalFilterChromeCompactor.ApplySlotId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            for (var col = placement.Col; col < placement.Col + placement.Span && col <= columnCount; col++)
            {
                tracks[col - 1] = "max-content";
            }
        }

        return $"{InteriorGridVariables(columnCount)}grid-template-columns:{string.Join(' ', tracks)};";
    }

    public static string SlotStyle(PlacementDefinition placement, int columns)
    {
        var span = Math.Min(placement.Span, columns);
        return placement.Row > 0
            ? $"grid-column:{placement.Col} / span {span};grid-row:{placement.Row};"
            : $"grid-column:span {span};";
    }
}
