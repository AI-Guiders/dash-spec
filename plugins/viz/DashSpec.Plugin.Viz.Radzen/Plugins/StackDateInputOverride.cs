using DashSpec.Execution.Runtime;
using DashSpec.Plugin.Filter.Builtins.Components;
using DashSpec.Plugin.Viz.RadzenUI.Components;

namespace DashSpec.Plugin.Viz.RadzenUI.Plugins;

/// <summary>
/// ADR-0118: on the Radzen stack the date selectors bind to native Radzen controls.
/// Step 1 of the small-steps rollout: day grain only; month/year/range keep the
/// flatpickr builtins until their steps land.
/// </summary>
public sealed class StackDateInputOverride : IFilterDateInputOverride
{
    public Type? ResolveDateInput(DateGrain grain) => grain switch
    {
        DateGrain.Day => typeof(RadzenDateInput),
        _ => null,
    };

    public Type? ResolveRangeInput() => null;
}
