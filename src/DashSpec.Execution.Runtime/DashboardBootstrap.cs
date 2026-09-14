using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Execution.Runtime;

public static class DashboardBootstrap
{
    public static FilterState CreateInitialFilters(DashboardDocument document, DateOnly todayUtc)
    {
        var state = new FilterState();
        foreach (var filter in document.Filters)
        {
            switch (filter.Kind)
            {
                case FilterKind.Date:
                    var range = DateDefaultRange.Resolve(filter.DefaultExpression!, todayUtc);
                    state.SetDate(filter.Name, range.From, range.To);
                    break;
                case FilterKind.Field:
                    state.SetField(filter.Name, FieldFilterDefaults.ResolveValues(filter.DefaultExpression));
                    break;
                case FilterKind.Top:
                    state.SetTop(filter.Name, TopLimitDefaults.Resolve(filter, null));
                    break;
            }
        }

        return state;
    }

    public static IReadOnlyDictionary<string, FilterDefinition> IndexFilters(DashboardDocument document) =>
        document.Filters.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
}
