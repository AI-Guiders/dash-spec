using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

/// <summary>ADR-0120 inert selection (ADR-0062): a value outside the current domain is
/// stored grey with a reason, never rides the compiled query, drops with one click,
/// and returns to active when the domain comes back.</summary>
public class FilterStateInertSelectionTests
{
    private const string Reason = "нет в выбранном периоде";

    [Fact]
    public void domain_shrink_moves_out_of_domain_values_to_inert_with_reason()
    {
        var state = new FilterState();
        state.SetField("product", ["nanoCAD", "AliveColors", "DeadProduct"]);

        state.ApplyDomain("product", ["nanoCAD", "AliveColors"], Reason);

        var value = state.GetFieldWithInert("product")!.Value;
        Assert.Equal(["nanoCAD", "AliveColors"], value.Values);             // active — rides SQL
        Assert.Equal(["DeadProduct"], value.InertValues);                   // grey
        Assert.Equal(Reason, value.InertReason);                            // tooltip
    }

    [Fact]
    public void inert_values_never_ride_the_compiled_query()
    {
        var state = new FilterState();
        state.SetField("product", ["nanoCAD", "DeadProduct"]);

        state.ApplyDomain("product", ["nanoCAD"], Reason);

        // the compiler reads GetField — it sees only active values by construction
        Assert.Equal(["nanoCAD"], state.GetField("product")!.Value.Values);
    }

    [Fact]
    public void one_click_clears_the_inert_residue()
    {
        var state = new FilterState();
        state.SetField("product", ["nanoCAD", "DeadProduct"]);
        state.ApplyDomain("product", ["nanoCAD"], Reason);

        state.ClearInert("product");

        var value = state.GetFieldWithInert("product")!.Value;
        Assert.False(value.HasInert);
        Assert.Equal(["nanoCAD"], value.Values);
    }

    [Fact]
    public void domain_return_restores_the_inert_value_to_active()
    {
        var state = new FilterState();
        state.SetField("product", ["nanoCAD", "DeadProduct"]);
        state.ApplyDomain("product", ["nanoCAD"], Reason);

        state.ApplyDomain("product", ["nanoCAD", "DeadProduct"], Reason);

        var value = state.GetFieldWithInert("product")!.Value;
        Assert.False(value.HasInert);
        Assert.Equal(["nanoCAD", "DeadProduct"], value.Values);            // clickable again
    }

    [Fact]
    public void fresh_selection_resets_inert()
    {
        var state = new FilterState();
        state.SetField("product", ["nanoCAD", "DeadProduct"]);
        state.ApplyDomain("product", ["nanoCAD"], Reason);

        state.SetField("product", ["AliveColors"]);

        var value = state.GetFieldWithInert("product")!.Value;
        Assert.False(value.HasInert);
    }

    [Fact]
    public void clone_preserves_the_inert_residue()
    {
        var state = new FilterState();
        state.SetField("product", ["nanoCAD", "DeadProduct"]);
        state.ApplyDomain("product", ["nanoCAD"], Reason);

        var copy = state.Clone();
        var value = copy.GetFieldWithInert("product")!.Value;

        Assert.Equal(["DeadProduct"], value.InertValues);
        Assert.Equal(Reason, value.InertReason);
    }

    [Fact]
    public void apply_domain_on_unknown_filter_is_a_noop()
    {
        var state = new FilterState();

        state.ApplyDomain("ghost", ["a"], Reason);

        Assert.Null(state.GetField("ghost"));
    }
}
