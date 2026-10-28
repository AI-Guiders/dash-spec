
using DashSpec.Modeling.Parse.Layout;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// Viewport-class variant planning ([ADR-0111] §7): the same placement under each viewport
/// class, derived once at assembly. The wide class keeps the author's grid as declared;
/// mid re-scales columns and spans proportionally into half the grid — slots keep their
/// declared order and relative width (no dense-flow reshuffles); narrow stacks full width.
/// The browser switches variants by media query — pure CSS, zero runtime computation.
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
            // Mid keeps the slot's declared column (rescaled) — auto-flow dense reordered
            // slots and stranded wide-content slots (heatmap right, dead space left).
            Mid = new PlacementDefinition(
                wide.Row,
                ScaleColumn(wide.Col, wideColumns, midColumns),
                ScaleSpan(wide.Span, wideColumns, midColumns)),
            Narrow = new PlacementDefinition(0, 0, 1),
        };
    }

    /// <summary>Proportional column re-scaling (1-based), never zero, never past the class grid.</summary>
    public static int ScaleColumn(int wideColumn, int wideColumns, int classColumns)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(wideColumns, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(classColumns, 1);
        var scaled = (int)Math.Round(wideColumn * (double)classColumns / wideColumns, MidpointRounding.AwayFromZero);
        return Math.Clamp(scaled, 1, classColumns);
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
