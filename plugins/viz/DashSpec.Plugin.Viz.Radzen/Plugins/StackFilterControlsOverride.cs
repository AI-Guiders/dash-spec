using DashSpec.Execution.Runtime;
using DashSpec.Plugin.Filter.Builtins.Components;
using DashSpec.Plugin.Viz.RadzenUI.Components;

namespace DashSpec.Plugin.Viz.RadzenUI.Plugins;

/// <summary>
/// ADR-0118: on the Radzen stack the selector roles bind to native Radzen controls —
/// selector/values to RadzenDropDown (single / collapsed Multiple / inline Chips),
/// the day grain to RadzenDatePicker, the range to RadzenDateRangePicker (stored-field
/// bind + debounced commit, one gesture = one apply). Month/year keep the flatpickr
/// builtins until their step lands.
/// </summary>
public sealed class StackFilterControlsOverride : IFilterSelectorOverride, IFilterDateInputOverride
{
    public Type? ResolveSelector(string density, bool singleSelect) => typeof(RadzenSelectorValues);

    public Type? ResolveDateInput(DateGrain grain) => grain switch
    {
        DateGrain.Day => typeof(RadzenDateInput),
        _ => null,
    };

    public Type? ResolveRangeInput() => typeof(RadzenRangeDateInput);
}
