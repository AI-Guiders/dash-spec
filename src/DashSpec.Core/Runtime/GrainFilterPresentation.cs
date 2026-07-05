using DashSpec.Core.Model;

namespace DashSpec.Core.Runtime;

/// <summary>UI labels and date parts for filters with <c>grain_filter</c>.</summary>
public static class GrainFilterPresentation
{
    private static readonly IReadOnlyDictionary<string, string> DefaultLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["day"] = "День",
            ["month"] = "Месяц",
            ["year"] = "Год",
        };

    public static string? ResolveGrain(
        FilterDefinition filter,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        IReadOnlyDictionary<string, HashSet<string>> selectedFields)
    {
        if (string.IsNullOrWhiteSpace(filter.GrainFilterName))
        {
            return null;
        }

        if (selectedFields.TryGetValue(filter.GrainFilterName, out var selected) &&
            selected.Count > 0)
        {
            return selected.First();
        }

        if (filterIndex.TryGetValue(filter.GrainFilterName, out var grainFilter) &&
            !string.IsNullOrWhiteSpace(grainFilter.DefaultExpression))
        {
            return grainFilter.DefaultExpression;
        }

        return "day";
    }

    public static string DisplayLabel(
        FilterDefinition filter,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        IReadOnlyDictionary<string, HashSet<string>> selectedFields)
    {
        if (string.IsNullOrWhiteSpace(filter.GrainFilterName))
        {
            return StaticLabel(filter);
        }

        var grain = ResolveGrain(filter, filterIndex, selectedFields);
        if (grain is not null &&
            TryResolveGrainLabel(filter, grain, out var grainLabel))
        {
            return grainLabel;
        }

        return StaticLabel(filter);
    }

    public static DateOnly NormalizeAnchor(DateOnly selected, string? grain) =>
        PeriodAnchorResolver.ResolveAnchor(selected, grain);

    public static string FormatChipValue(DateOnly from, DateOnly to, string? grain) =>
        grain?.Trim().ToLowerInvariant() switch
        {
            "month" => from.ToString("yyyy-MM"),
            "year" => from.Year.ToString(),
            _ => from == to ? from.ToString("yyyy-MM-dd") : $"{from:yyyy-MM-dd}…{to:yyyy-MM-dd}",
        };

    public static void NormalizeAnchoredDates(
        string grainFilterName,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        IReadOnlyDictionary<string, HashSet<string>> selectedFields,
        IDictionary<string, DateOnly> dateFrom,
        IDictionary<string, DateOnly> dateTo)
    {
        foreach (var filter in AnchoredDateFilters(grainFilterName, filterIndex))
        {
            if (!dateFrom.TryGetValue(filter.Name, out var from))
            {
                continue;
            }

            var grain = ResolveGrain(filter, filterIndex, selectedFields);
            var anchor = NormalizeAnchor(from, grain);
            dateFrom[filter.Name] = anchor;
            dateTo[filter.Name] = anchor;
        }
    }

    public static bool IsGrainHostFilter(
        string filterName,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex) =>
        filterIndex.Values.Any(filter =>
            string.Equals(filter.GrainFilterName, filterName, StringComparison.OrdinalIgnoreCase));

    public static void SnapAnchoredDates(
        string grainFilterName,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        IReadOnlyDictionary<string, HashSet<string>> selectedFields,
        IDictionary<string, DateOnly> dateFrom,
        IDictionary<string, DateOnly> dateTo,
        DateOnly referenceDay)
    {
        foreach (var filter in AnchoredDateFilters(grainFilterName, filterIndex))
        {
            var grain = ResolveGrain(filter, filterIndex, selectedFields);
            var anchor = NormalizeAnchor(referenceDay, grain);
            dateFrom[filter.Name] = anchor;
            dateTo[filter.Name] = anchor;
        }
    }

    private static IEnumerable<FilterDefinition> AnchoredDateFilters(
        string grainFilterName,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex) =>
        filterIndex.Values.Where(filter =>
            string.Equals(filter.GrainFilterName, grainFilterName, StringComparison.OrdinalIgnoreCase));

    private static bool TryResolveGrainLabel(
        FilterDefinition filter,
        string grain,
        out string label)
    {
        if (filter.GrainLabels is not null &&
            filter.GrainLabels.TryGetValue(grain, out var custom) &&
            !string.IsNullOrWhiteSpace(custom))
        {
            label = custom;
            return true;
        }

        if (DefaultLabels.TryGetValue(grain, out var fallback))
        {
            label = fallback;
            return true;
        }

        label = string.Empty;
        return false;
    }

    private static string StaticLabel(FilterDefinition filter) =>
        !string.IsNullOrWhiteSpace(filter.Label)
            ? filter.Label
            : filter.Name.Replace('_', ' ');
}
