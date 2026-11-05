
using DashSpec.Modeling.Parse.Layout;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// Viewport-class variant planning ([ADR-0111] §7) for **declared boards** (page/tab grids
/// with explicit cells): the wide class keeps the author's grid as declared; mid re-scales
/// spans into half the columns and lets the grid auto-flow (dense) — dense never overlaps,
/// so declared boards need no packing; narrow stacks everything full width. Card interiors
/// are not this path — auto interiors are a flex flow ([ADR-0116]). The browser switches
/// variants by media query — pure CSS, zero runtime computation.
/// </summary>
public static class PlacementVariantsPlanning
{
    public static int MidColumns(int wideColumns)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(wideColumns, 1);
        return Math.Max(1, wideColumns / 2);
    }

    public static int NarrowColumns(int wideColumns)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(wideColumns, 1);
        return 1;
    }

    public static PlacementVariants For(PlacementDefinition wide, int wideColumns)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(wideColumns, 1);
        var midColumns = MidColumns(wideColumns);
        return new PlacementVariants
        {
            Wide = wide,
            Mid = new PlacementDefinition(0, 0, ScaleSpan(wide.Span, wideColumns, midColumns)),
            Narrow = new PlacementDefinition(0, 0, 1),
        };
    }

    /// <summary>Proportional span re-scaling, never zero, never past the class grid.</summary>
    public static int ScaleSpan(int wideSpan, int wideColumns, int classColumns)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(wideColumns, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(classColumns, 1);
        var scaled = (int)Math.Round(wideSpan * (double)classColumns / wideColumns, MidpointRounding.AwayFromZero);
        return Math.Clamp(scaled, 1, classColumns);
    }

    /// <summary>
    /// Fixed-mid variant for one-row shapes whose tracks are not proportional (the card
    /// filter chrome with its max-content apply column, ADR-0061): mid keeps the declared
    /// cells, narrow stacks full width.
    /// </summary>
    public static PlacementVariants ForFixedMid(PlacementDefinition wide) =>
        new()
        {
            Wide = wide,
            Mid = wide,
            Narrow = new PlacementDefinition(0, 0, 1),
        };
}
