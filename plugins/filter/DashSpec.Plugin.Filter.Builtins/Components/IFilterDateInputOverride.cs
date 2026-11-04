using DashSpec.Execution.Runtime;

namespace DashSpec.Plugin.Filter.Builtins.Components;

/// <summary>
/// ADR-0118: the inner date controls of the date selectors are stack-replaceable.
/// Filter.Builtins owns the semantics and the default flatpickr implementations; a
/// stack may register control types implementing the same parameter surface
/// (Grain, wire Value yyyy-MM-dd | yyyy-MM | yyyy, ValueChanged&lt;string&gt;,
/// Disabled, InputClass, AriaLabel). Absent override — the builtins render, exactly
/// as before. The grain crosses the seam as the <see cref="DateGrain"/> enum; the
/// wire string stays the shared contract.
/// </summary>
public interface IFilterDateInputOverride
{
    /// <summary>Component for the single-date input surface per grain. Null — keep
    /// the flatpickr control (CultureAwareDateInput).</summary>
    Type? ResolveDateInput(DateGrain grain);

    /// <summary>Component for the range surface (From/To wire strings,
    /// RangePicked (From, To), Disabled, InputClass, AriaLabel). Null — keep the
    /// flatpickr control (RangeDateInput).</summary>
    Type? ResolveRangeInput();
}

/// <summary>Default: no overrides (the builtin flatpickr controls render).</summary>
public sealed class DefaultFilterDateInputOverride : IFilterDateInputOverride
{
    public Type? ResolveDateInput(DateGrain grain) => null;

    public Type? ResolveRangeInput() => null;
}
