using DashSpec.Core.Model;

namespace DashSpec.Core.Layout;

/// <summary>
/// When a page derives a date filter, expose the source + grain controls on toolbar
/// instead of the derived target (ADR-0036 parity).
/// </summary>
public static class DeriveToolbarExpander
{
    public static IReadOnlyList<string> Expand(
        IReadOnlyList<string> visibleFilters,
        FilterDeriveDefinition? derive,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex)
    {
        if (derive is null || visibleFilters.Count == 0)
        {
            return visibleFilters;
        }

        var expanded = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in visibleFilters)
        {
            if (string.Equals(name, derive.TargetFilter, StringComparison.OrdinalIgnoreCase))
            {
                AddDeriveControls(expanded, seen, derive, filterIndex);
                continue;
            }

            Add(expanded, seen, name);
        }

        if (!visibleFilters.Any(name =>
                string.Equals(name, derive.TargetFilter, StringComparison.OrdinalIgnoreCase)))
        {
            AddDeriveControls(expanded, seen, derive, filterIndex);
        }

        return expanded;
    }

    static void AddDeriveControls(
        List<string> expanded,
        HashSet<string> seen,
        FilterDeriveDefinition derive,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex)
    {
        if (!string.IsNullOrWhiteSpace(derive.GrainFilterName)
            && filterIndex.ContainsKey(derive.GrainFilterName))
        {
            Add(expanded, seen, derive.GrainFilterName);
        }

        if (!string.IsNullOrWhiteSpace(derive.SourceFilter)
            && filterIndex.ContainsKey(derive.SourceFilter))
        {
            Add(expanded, seen, derive.SourceFilter);
        }
    }

    static void Add(List<string> expanded, HashSet<string> seen, string name)
    {
        if (seen.Add(name))
        {
            expanded.Add(name);
        }
    }
}
