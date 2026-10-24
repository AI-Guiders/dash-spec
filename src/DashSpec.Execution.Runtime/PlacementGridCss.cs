
using DashSpec.Modeling.Parse.Layout;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// CSS projection of the slot plane ([ADR-0111] §4/§7): the assembled placements ride to the
/// DOM as custom properties — one whole grid-track value per viewport class — and the stacks'
/// CSS resolves them per media query. No final grid properties inline: the class switch is a
/// stylesheet decision, not a style-attribute one.
/// </summary>
public static class PlacementGridCss
{
    /// <summary>
    /// Grid variables for host/toolbar grids ([ADR-0111] §7: the gap is fluid in CSS —
    /// <c>clamp()</c> — not a fixed pixel here; the track counts are per viewport class).
    /// </summary>
    public static string HostGridVariables(LayoutDefinition layout)
    {
        var columns = layout.Columns;
        return "--grid-columns:" + columns + ";" + TrackVariables(columns);
    }

    public static string InteriorGridVariables(int columns) =>
        $"--card-grid-columns:{columns};" + InteriorTrackVariables(columns);

    public static string GridVariables(LayoutSlotScope scope, LayoutDefinition layout) =>
        scope switch
        {
            LayoutSlotScope.HostTabBoard or LayoutSlotScope.HostPageToolbar => HostGridVariables(layout),
            _ => InteriorGridVariables(layout.Columns),
        };

    /// <summary>Card filter chrome row: apply slot column shrinks to icon (ADR-0061).
    /// Mid keeps the wide row (the controls are small); narrow stacks full width.</summary>
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

        var wide = string.Join(' ', tracks);
        // Mid: same one-row shape (re-scaling would break the max-content apply column);
        // the row is small enough to keep its cells. Narrow: everything stacks.
        return $"--card-grid-columns:{columnCount};--tracks:{wide};" +
               $"--tracks-mid:{wide};--tracks-narrow:minmax(0,1fr);";
    }

    /// <summary>
    /// One slot's placement per viewport class as custom properties — whole grid-track
    /// values (<c>--gc: 10 / span 3</c>, <c>--gc-mid: auto / span 2</c>, …). The stacks' CSS
    /// resolves <c>grid-column</c>/<c>grid-row</c> from them per media query.
    /// </summary>
    public static string SlotStyle(PlacementVariants variants) =>
        $"--gc:{ColumnValue(variants.Wide)};--gr:{RowValue(variants.Wide)};" +
        $"--gc-mid:{ColumnValue(variants.Mid)};--gr-mid:{RowValue(variants.Mid)};" +
        $"--gc-narrow:{ColumnValue(variants.Narrow)};--gr-narrow:{RowValue(variants.Narrow)};";

    /// <summary>Group/nest outer frame: a full-width row at every class; only the wide
    /// class keeps the declared row (mid/narrow auto-flow with the board).</summary>
    public static string GroupOuterStyle(int outerRow) =>
        $"--gc:1 / -1;--gr:{(outerRow > 0 ? $"{outerRow}" : "auto")};" +
        "--gc-mid:1 / -1;--gr-mid:auto;" +
        "--gc-narrow:1 / -1;--gr-narrow:auto;";

    static string ColumnValue(PlacementDefinition placement) =>
        placement.Col > 0
            ? $"{placement.Col} / span {Math.Max(placement.Span, 1)}"
            : $"auto / span {Math.Max(placement.Span, 1)}";

    static string RowValue(PlacementDefinition placement) =>
        placement.Row > 0 ? $"{placement.Row}" : "auto";

    static string TrackVariables(int columns)
    {
        var mid = PlacementVariantsPlanning.MidColumns(columns);
        return $"--tracks:repeat({columns}, minmax(0,1fr));" +
               $"--tracks-mid:repeat({mid}, minmax(0,1fr));" +
               "--tracks-narrow:repeat(1, minmax(0,1fr));";
    }

    static string InteriorTrackVariables(int columns)
    {
        var mid = PlacementVariantsPlanning.MidColumns(columns);
        return $"--card-tracks:repeat({columns}, minmax(0,1fr));" +
               $"--card-tracks-mid:repeat({mid}, minmax(0,1fr));" +
               "--card-tracks-narrow:repeat(1, minmax(0,1fr));";
    }
}
