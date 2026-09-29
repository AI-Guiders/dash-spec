using DashSpec.Core.Model;
using DashSpec.Execution.Runtime;

namespace DashSpec.Filters;

public static class FilterWidgetPresentation
{
    public static string DisplayLabel(
        FilterDefinition filter,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        IReadOnlyDictionary<string, HashSet<string>> selectedFields) =>
        GrainFilterPresentation.DisplayLabel(filter, filterIndex, selectedFields);

    public static int ResolveTopValue(
        FilterDefinition filter,
        IReadOnlyDictionary<string, int> topLimits)
    {
        if (filter.Kind is not FilterKind.Top)
        {
            return 0;
        }

        return TopLimitDefaults.Resolve(
            filter,
            topLimits.TryGetValue(filter.Name, out var current) ? current : null);
    }

    public static HashSet<string> SelectedFieldValues(
        FilterDefinition filter,
        IReadOnlyDictionary<string, HashSet<string>> selectedFields)
    {
        if (filter.Kind is not FilterKind.Field)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return selectedFields.TryGetValue(filter.Name, out var selected)
            ? selected
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }
}
