using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Execution.Runtime;

public static class DashboardBootstrap
{
    public static FilterState CreateInitialFilters(
        DashboardDocument document,
        DateOnly todayUtc,
        IReadOnlyDictionary<string, string>? filterDefaultOverrides = null)
    {
        var state = new FilterState();
        foreach (var filter in document.Filters)
        {
            var defaultExpression = ResolveDefaultExpression(filter, filterDefaultOverrides);
            if (string.IsNullOrWhiteSpace(defaultExpression))
            {
                continue;
            }

            switch (filter.Kind)
            {
                case FilterKind.Date:
                    var range = DateDefaultRange.Resolve(defaultExpression, todayUtc);
                    state.SetDate(filter.Name, range.From, range.To);
                    break;
                case FilterKind.Field:
                    state.SetField(filter.Name, FieldFilterDefaults.ResolveValues(defaultExpression));
                    break;
                case FilterKind.Top:
                    state.SetTop(filter.Name, TopLimitDefaults.Resolve(filter with { DefaultExpression = defaultExpression }, null));
                    break;
            }
        }

        return state;
    }

    public static string? ResolveDefaultExpression(
        FilterDefinition filter,
        IReadOnlyDictionary<string, string>? overrides = null)
    {
        if (overrides is not null &&
            overrides.TryGetValue(filter.Name, out var overrideValue) &&
            !string.IsNullOrWhiteSpace(overrideValue))
        {
            return overrideValue;
        }

        return filter.DefaultExpression;
    }

    public static IReadOnlyDictionary<string, FilterDefinition> IndexFilters(DashboardDocument document) =>
        document.Filters.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
}
