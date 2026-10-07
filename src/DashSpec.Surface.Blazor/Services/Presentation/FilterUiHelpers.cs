using DashSpec.Core.Model;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
using DashSpec.Filters;

namespace DashSpec.Surface.Blazor.Services.Presentation;

internal static class FilterUiHelpers
{
    public static string DisplayLabel(
        FilterDefinition filter,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        IReadOnlyDictionary<string, HashSet<string>> selectedFields) =>
        FilterWidgetPresentation.DisplayLabel(filter, filterIndex, selectedFields);

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

    public static string ScopeHintAriaLabel(
        string filterName,
        IReadOnlyDictionary<string, IReadOnlyList<string>> filtersToCards)
    {
        if (!filtersToCards.TryGetValue(filterName, out var cards) || cards.Count == 0)
        {
            return "Область фильтра: ни одна карточка";
        }

        return cards.Count == 1
            ? $"Область фильтра: 1 карточка ({cards[0]})"
            : $"Область фильтра: {cards.Count} карточек";
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
        IReadOnlyDictionary<string, int> topLimits) =>
        FilterWidgetPresentation.ResolveTopValue(filter, topLimits);

    public static HashSet<string> SelectedFieldValues(
        FilterDefinition filter,
        IReadOnlyDictionary<string, HashSet<string>> selectedFields) =>
        FilterWidgetPresentation.SelectedFieldValues(filter, selectedFields);
}
