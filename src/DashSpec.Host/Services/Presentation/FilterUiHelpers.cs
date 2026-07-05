using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Host.Services.Presentation;

internal static class FilterUiHelpers
{
    public static string DisplayLabel(
        FilterDefinition filter,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        IReadOnlyDictionary<string, HashSet<string>> selectedFields) =>
        GrainFilterPresentation.DisplayLabel(filter, filterIndex, selectedFields);

    public static string DisplayLabel(FilterDefinition filter) =>
        GrainFilterPresentation.DisplayLabel(
            filter,
            new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase));

    public static string ScopeHint(
        string filterName,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filtersToCards)
    {
        if (!filtersToCards.TryGetValue(filterName, out var cards) || cards.Count == 0)
        {
            return "ни одна карточка";
        }

        return string.Join(", ", cards);
    }

    public static string FormatActiveChip(
        string filterName,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        IReadOnlyDictionary<string, DateOnly> dateFrom,
        IReadOnlyDictionary<string, DateOnly> dateTo,
        IReadOnlyDictionary<string, HashSet<string>> selectedFields)
    {
        if (!filterIndex.TryGetValue(filterName, out var filter))
        {
            return filterName;
        }

        var label = DisplayLabel(filter, filterIndex, selectedFields);
        if (filter.Kind is FilterKind.Date &&
            dateFrom.TryGetValue(filterName, out var from) &&
            dateTo.TryGetValue(filterName, out var to))
        {
            var grain = GrainFilterPresentation.ResolveGrain(filter, filterIndex, selectedFields);
            var value = GrainFilterPresentation.FormatChipValue(from, to, grain);
            return $"{label}: {value}";
        }

        if (filter.Kind is FilterKind.Field &&
            selectedFields.TryGetValue(filterName, out var selected))
        {
            return selected.Count == 0
                ? $"{label}: все"
                : $"{label}: {string.Join(", ", selected.Take(3))}{(selected.Count > 3 ? $" +{selected.Count - 3}" : "")}";
        }

        return label;
    }

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
