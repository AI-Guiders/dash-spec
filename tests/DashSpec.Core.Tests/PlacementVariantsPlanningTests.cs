using DashSpec.Execution.Runtime;
using DashSpec.Modeling.Parse.Layout;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace DashSpec.Core.Tests;

/// <summary>
/// Viewport-class variant planning ([ADR-0111] §7) — property-based per [ADR-0113]:
/// the planning is a pure normalization, so its invariants are stated as laws over
/// generated placements, with fixed-string contracts kept as regression examples.
/// </summary>
public class PlacementVariantsPlanningTests
{
    private static Gen<(int Columns, int Span, int Col, int Row)> Placements() =>
        from columns in Gen.Choose(1, 24)
        from span in Gen.Choose(1, columns)
        from col in Gen.Choose(0, columns)
        from row in Gen.Choose(0, 3)
        select (columns, span, col, row);

    [Property]
    public Property Class_variants_never_escape_their_class_grid()
    {
        return Prop.ForAll(Placements().ToArbitrary(), t =>
        {
            var (columns, span, col, row) = t;
            var variants = PlacementVariantsPlanning.For(new PlacementDefinition(row, col, span), columns);

            // NB: F# records from C# — `==` is reference equality, structural comparison is .Equals.
            return (variants.Wide.Equals(new PlacementDefinition(row, col, span)))
                .Label("wide is the declared placement")
                .And(variants.Mid.Span >= 1).Label("mid span at least one")
                .And(variants.Mid.Span <= PlacementVariantsPlanning.MidColumns(columns)).Label("mid span within mid grid")
                .And(variants.Mid.Row == row).Label("mid keeps the declared row")
                .And((col == 0 || (variants.Mid.Col >= 1 && variants.Mid.Col <= PlacementVariantsPlanning.MidColumns(columns))))
                    .Label("mid keeps declared columns rescaled, auto cells stay auto")
                .And(variants.Narrow.Equals(new PlacementDefinition(0, 0, 1))).Label("narrow stacks full width");
        });
    }

    [Property]
    public Property Full_width_placement_stays_full_width_in_every_class()
    {
        return Prop.ForAll(Gen.Choose(1, 24).ToArbitrary(), columns =>
        {
            var variants = PlacementVariantsPlanning.For(new PlacementDefinition(0, 0, columns), columns);

            return (variants.Mid.Span == PlacementVariantsPlanning.MidColumns(columns))
                .Label("mid span equals the mid grid")
                .And(variants.Narrow.Span == 1).Label("narrow span equals the narrow grid");
        });
    }

    [Property]
    public Property Span_scaling_is_monotonic_in_the_declared_span()
    {
        var spans = from columns in Gen.Choose(1, 24)
                    from left in Gen.Choose(1, columns)
                    from right in Gen.Choose(left, columns)
                    select (columns, left, right);

        return Prop.ForAll(spans.ToArbitrary(), t =>
        {
            var (columns, left, right) = t;
            var mid = PlacementVariantsPlanning.MidColumns(columns);

            return (PlacementVariantsPlanning.ScaleSpan(left, columns, mid)
                <= PlacementVariantsPlanning.ScaleSpan(right, columns, mid))
                .Label($"span {left} scales to no more than span {right}");
        });
    }

    [Property]
    public Property Slot_style_projects_all_three_classes_and_no_final_grid_properties()
    {
        return Prop.ForAll(Placements().ToArbitrary(), t =>
        {
            var (columns, span, col, row) = t;
            var style = PlacementGridCss.SlotStyle(
                PlacementVariantsPlanning.For(new PlacementDefinition(row, col, span), columns));

            return style.StartsWith("--gc:").Label("wide column first")
                .And(style.Contains("--gc-mid:") && style.Contains("--gc-narrow:")).Label("mid and narrow columns present")
                .And(style.Contains("--gr:") && style.Contains("--gr-mid:") && style.Contains("--gr-narrow:")).Label("rows per class present")
                .And(!style.Contains("grid-column:")).Label("no final grid-column")
                .And(!style.Contains("grid-row:")).Label("no final grid-row");
        });
    }

    [Property]
    public Property Container_variables_carry_track_counts_per_class()
    {
        return Prop.ForAll(Gen.Choose(1, 24).ToArbitrary(), columns =>
        {
            var mid = PlacementVariantsPlanning.MidColumns(columns);
            var host = PlacementGridCss.HostGridVariables(new LayoutDefinition { Columns = columns });
            var interior = PlacementGridCss.InteriorGridVariables(columns);

            return host.Contains($"--grid-columns:{columns};").Label("host column count")
                .And(host.Contains($"--tracks:repeat({columns}, minmax(0,1fr));")).Label("wide tracks")
                .And(host.Contains($"--tracks-mid:repeat({mid}, minmax(0,1fr));")).Label("mid tracks")
                .And(host.Contains("--tracks-narrow:repeat(1, minmax(0,1fr));")).Label("narrow tracks")
                .And(interior.Contains($"--card-tracks:repeat({columns}, minmax(0,1fr));")).Label("interior wide tracks")
                .And(interior.Contains($"--card-tracks-mid:repeat({mid}, minmax(0,1fr));")).Label("interior mid tracks");
        });
    }

    [Fact]
    public void Wide_declared_cells_render_as_declared_and_auto_cells_as_auto()
    {
        var declared = PlacementGridCss.SlotStyle(
            PlacementVariantsPlanning.For(new PlacementDefinition(2, 3, 4), 12));

        Assert.Contains("--gc:3 / span 4;", declared);
        Assert.Contains("--gr:2;", declared);
        // mid keeps the declared cell, rescaled into half the grid: col 3→2, span 4→2
        Assert.Contains("--gc-mid:2 / span 2;", declared);
        Assert.Contains("--gr-mid:2;", declared);
        Assert.Contains("--gc-narrow:auto / span 1;", declared);

        var defaulted = PlacementGridCss.SlotStyle(
            PlacementVariantsPlanning.For(new PlacementDefinition(0, 0, 4), 12));

        Assert.Contains("--gc:auto / span 4;", defaulted);
        Assert.Contains("--gr:auto;", defaulted);
    }

    [Fact]
    public void Group_outer_frame_is_full_width_everywhere_and_auto_flows_after_wide()
    {
        var style = PlacementGridCss.GroupOuterStyle(3);

        Assert.Contains("--gc:1 / -1;", style);
        Assert.Contains("--gr:3;", style);
        Assert.Contains("--gc-mid:1 / -1;", style);
        Assert.Contains("--gr-mid:auto;", style);
        Assert.Contains("--gc-narrow:1 / -1;", style);
    }

    [Fact]
    public void Chrome_variants_keep_the_declared_cells_through_mid()
    {
        var variants = PlacementVariantsPlanning.ForFixedMid(new PlacementDefinition(1, 3, 1));

        Assert.Equal(variants.Wide, variants.Mid);
        Assert.Equal(new PlacementDefinition(0, 0, 1), variants.Narrow);
    }

    [Fact]
    public void Chrome_grid_keeps_max_content_apply_column_and_stacks_narrow()
    {
        var placements = new Dictionary<string, PlacementDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["activity_slot"] = new(1, 1, 2),
            [CardLocalFilterChromeCompactor.ApplySlotId] = new(1, 3, 1),
            ["app_name"] = new(1, 4, 1),
        };

        var style = PlacementGridCss.ChromeGridStyle(placements, 4);

        Assert.Contains("max-content", style);
        Assert.Contains("--card-grid-columns:4;", style);
        Assert.Contains("--tracks-narrow:minmax(0,1fr);", style);
    }
}
