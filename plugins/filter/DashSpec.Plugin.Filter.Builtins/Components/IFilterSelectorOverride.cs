using DashSpec.Plugin.Filter.Builtins.Components.Shared;

namespace DashSpec.Plugin.Filter.Builtins.Components;

/// <summary>
/// ADR-0118: the inner selector/values control (combobox | chips | select) is
/// stack-replaceable. Filter.Builtins owns the semantics and the default markup
/// controls; a stack may register its own control types implementing the same
/// parameter surface (Options / Selected / SelectedChanged / Disabled / SingleSelect /
/// Density / Label). Absent override — the builtins render, exactly as before.
/// </summary>
public interface IFilterSelectorOverride
{
    /// <summary>Component for FieldMultiSelect's surface. Density: inline | collapsed.
    /// Null — keep the builtin control for this density.</summary>
    Type? ResolveSelector(string density, bool singleSelect);
}

/// <summary>Default: no overrides (the builtin markup controls render).</summary>
public sealed class DefaultFilterSelectorOverride : IFilterSelectorOverride
{
    public Type? ResolveSelector(string density, bool singleSelect) => null;
}

/// <summary>Current override holder (the stack plugin sets it at startup; last wins).</summary>
public static class FilterSelectorControls
{
    private static readonly Lock Gate = new();

    public static IFilterSelectorOverride Current
    {
        get
        {
            lock (Gate)
            {
                return field;
            }
        }
        private set
        {
            lock (Gate)
            {
                field = value;
            }
        }
    } = new DefaultFilterSelectorOverride();

    public static void Set(IFilterSelectorOverride @override)
    {
        ArgumentNullException.ThrowIfNull(@override);
        Current = @override;
    }
}
