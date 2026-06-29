using DashSpec.Core.Model;

namespace DashSpec.Host.Services.Presentation;

internal static class FilterUiHelpers
{
    public static string DisplayLabel(FilterDefinition filter) =>
        !string.IsNullOrWhiteSpace(filter.Label)
            ? filter.Label
            : filter.Name.Replace('_', ' ');

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

        var label = DisplayLabel(filter);
        if (filter.Kind is FilterKind.Date &&
            dateFrom.TryGetValue(filterName, out var from) &&
            dateTo.TryGetValue(filterName, out var to))
        {
            return $"{label}: {from:yyyy-MM-dd}…{to:yyyy-MM-dd}";
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
}
