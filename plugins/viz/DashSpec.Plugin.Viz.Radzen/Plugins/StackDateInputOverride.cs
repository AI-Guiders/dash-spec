using DashSpec.Plugin.Filter.Builtins.Components;

namespace DashSpec.Plugin.Viz.RadzenUI.Plugins;

/// <summary>ADR-0118: the Radzen stack's date-control binding — the selector/dates
/// role renders RadzenDatePicker-backed controls (theme-painted by the stack; flatpickr
/// carries no skin here). Day/month/year over one control; the dual range input is
/// two pickers wired to the same range pipeline.</summary>
public sealed class StackFilterControlsOverride : IFilterDateInputOverride, IFilterSelectorOverride
{
    public Type? ResolveDateInput(string mode) => mode switch
    {
        "day" or "month" or "year" => typeof(Components.RadzenDateInput),
        _ => null,
    };

    public Type? ResolveRangeInput() => typeof(Components.RadzenRangeDateInput);

    /// <summary>selector/values: ONE RadzenDropDown per density (Multiple for
    /// collapsed, Chips=true for inline, single-select without Multiple).
    /// Density parameter rides the parameter surface; the type is the same.</summary>
    public Type? ResolveSelector(string density, bool singleSelect) =>
        typeof(Components.RadzenSelectorValues);
}
