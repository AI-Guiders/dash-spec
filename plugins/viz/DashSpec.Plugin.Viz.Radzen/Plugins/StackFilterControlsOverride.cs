using DashSpec.Execution.Runtime;
using DashSpec.Plugin.Filter.Builtins.Components;
using DashSpec.Plugin.Viz.RadzenUI.Components;

namespace DashSpec.Plugin.Viz.RadzenUI.Plugins;

/// <summary>
/// ADR-0118: on the Radzen stack the selector roles bind to native Radzen controls —
/// selector/values to RadzenDropDown (single / collapsed Multiple / inline Chips),
/// every date grain (day/month/year) to RadzenDatePicker (wire yyyy-MM-dd | yyyy-MM |
/// yyyy), the range to RadzenDateRangePicker (stored-field bind + debounced commit,
/// one gesture = one apply).
/// </summary>
public sealed class StackFilterControlsOverride : IFilterSelectorOverride, IFilterDateInputOverride
{
    public Type? ResolveSelector(string density, bool singleSelect) => typeof(RadzenSelectorValues);

    public Type? ResolveDateInput(DateGrain grain) => typeof(RadzenDateInput);

    public Type? ResolveRangeInput() => typeof(RadzenRangeDateInput);
}
