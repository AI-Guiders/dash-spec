using DashSpec.Plugin.Filter.Builtins.Components;

namespace DashSpec.Plugin.Viz.RadzenUI.Plugins;

/// <summary>ADR-0118: the Radzen stack's date-control binding — the selector/dates
/// role renders RadzenDatePicker-backed controls (theme-painted by the stack; flatpickr
/// carries no skin here). Day/month/year over one control; the dual range input is
/// two pickers wired to the same range pipeline.</summary>
public sealed class StackDateInputOverride : IFilterDateInputOverride
{
    public Type? ResolveDateInput(string mode) => mode switch
    {
        "day" or "month" or "year" => typeof(Components.RadzenDateInput),
        _ => null,
    };

    public Type? ResolveRangeInput() => typeof(Components.RadzenRangeDateInput);
}
