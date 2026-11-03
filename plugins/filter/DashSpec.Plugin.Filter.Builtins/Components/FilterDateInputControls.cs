using DashSpec.Plugin.Filter.Builtins.Components;
using DashSpec.Plugin.Filter.Builtins.Components.Shared;

namespace DashSpec.Plugin.Filter.Builtins.Components;

/// <summary>Resolves the concrete inner date-control component per ADR-0118 override:
/// a stack-registered IFilterDateInputOverride wins, otherwise the builtin flatpickr
/// controls. Shared so GrainAwareDateInput and the range wrappers swap implementations
/// without knowing stacks.</summary>
public static class FilterDateInputControls
{
    private static readonly object Lock = new();

    private static IFilterDateInputOverride _override = new DefaultFilterDateInputOverride();

    public static IFilterDateInputOverride Current
    {
        get
        {
            lock (Lock)
            {
                return _override;
            }
        }
    }

    public static void Set(IFilterDateInputOverride @override)
    {
        ArgumentNullException.ThrowIfNull(@override);
        lock (Lock)
        {
            _override = @override;
        }
    }

    public static Type? DateInput(string mode) => Current.ResolveDateInput(mode);

    public static Type? RangeInput() => Current.ResolveRangeInput();
}
