using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Execution.Runtime;

/// <summary>Merges dashboard session filters with card-local values for SQL compile.</summary>
public static class CardQueryFilterState
{
    public static FilterState Compose(
        FilterState sessionFilters,
        CardDefinition card,
        IReadOnlyDictionary<string, FilterDefinition> filterIndex,
        IReadOnlyDictionary<string, DateRangeValue>? cardLocalDates,
        IReadOnlyDictionary<string, FieldFilterValue>? cardLocalFields,
        IReadOnlyDictionary<string, int>? cardLocalTop)
    {
        var result = sessionFilters.Clone();
        if (card.LocalFilters is not { Count: > 0 })
        {
            return result;
        }

        foreach (var filterName in card.LocalFilters)
        {
            if (!filterIndex.TryGetValue(filterName, out var filter))
            {
                continue;
            }

            switch (filter.Kind)
            {
                case FilterKind.Date when cardLocalDates?.TryGetValue(filterName, out var range) == true:
                    result.SetDate(filterName, range.From, range.To);
                    break;
                case FilterKind.Field when cardLocalFields?.TryGetValue(filterName, out var field) == true:
                    result.SetField(filterName, field.Values.ToList());
                    break;
                case FilterKind.Top when cardLocalTop?.TryGetValue(filterName, out var top) == true:
                    result.SetTop(filterName, top);
                    break;
            }
        }

        return result;
    }
}
