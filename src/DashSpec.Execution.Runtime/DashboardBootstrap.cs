using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Execution.Runtime;

public static class DashboardBootstrap
{
    public static FilterState CreateInitialFilters(
        DashboardDocument document,
        DateOnly todayUtc,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>? filterDefaultOverrides = null)
    {
        var state = new FilterState();
        foreach (var filter in document.Filters)
        {
            IReadOnlyDictionary<string, string>? scopedOverrides = null;
            if (filterDefaultOverrides is not null)
            {
                filterDefaultOverrides.TryGetValue(filter.Name, out scopedOverrides);
            }

            var defaultExpression = ResolveDefaultExpression(filter, scopedOverrides);
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
        IReadOnlyDictionary<string, string>? propertyOverrides = null)
    {
        if (propertyOverrides is not null)
        {
            var scoped = ResolveInitialExpression(filter.Kind, propertyOverrides);
            if (!string.IsNullOrWhiteSpace(scoped))
            {
                return scoped;
            }
        }

        return filter.DefaultExpression;
    }

    private static string? ResolveInitialExpression(
        FilterKind kind,
        IReadOnlyDictionary<string, string> properties)
    {
        foreach (var candidate in InitialExpressionCandidates(kind))
        {
            if (properties.TryGetValue(candidate, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static IEnumerable<string> InitialExpressionCandidates(FilterKind kind) =>
        kind switch
        {
            FilterKind.Date => ["range", "value"],
            FilterKind.Field => ["value", "scale", "selection"],
            FilterKind.Top => ["limit", "value"],
            _ => [],
        };

    public static IReadOnlyDictionary<string, FilterDefinition> IndexFilters(DashboardDocument document) =>
        document.Filters.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
}
