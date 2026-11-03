using DashSpec.Plugin.Filter.Builtins.Components.Shared;

namespace DashSpec.Plugin.Filter.Builtins.Components;

/// <summary>
/// ADR-0118: the inner date controls of the date selectors (day/month/year input and
/// the dual range input) are stack-replaceable. Filter.Builtins owns the semantics and
/// the default flatpickr implementations; a stack may register its own control types
/// implementing the same parameter surfaces (CultureAwareDateInput / RangeDateInput).
/// Absent override — the builtins render, exactly as before.
/// </summary>
public interface IFilterDateInputOverride
{
    /// <summary>Component for CultureAwareDateInput's surface (Mode day | month | year,
    /// wire Value yyyy-MM-dd | yyyy-MM | yyyy, ValueChanged&lt;string&gt;, Disabled,
    /// InputClass, AriaLabel). Null — keep the flatpickr control.</summary>
    Type? ResolveDateInput(string mode);

    /// <summary>Component for RangeDateInput's surface (From/To wire strings,
    /// RangePicked (From, To), Disabled, InputClass, AriaLabel). Null — keep the
    /// flatpickr control.</summary>
    Type? ResolveRangeInput();
}

/// <summary>Default: no overrides (the builtin flatpickr controls render).</summary>
public sealed class DefaultFilterDateInputOverride : IFilterDateInputOverride
{
    public Type? ResolveDateInput(string mode) => null;

    public Type? ResolveRangeInput() => null;
}
