namespace DashSpec.Core.Runtime;

public static class DashboardBootstrap
{
    public static FilterState CreateInitialFilters(Model.DashboardDocument document, DateOnly todayUtc)
    {
        var state = new FilterState();
        foreach (var filter in document.Filters)
        {
            switch (filter.Kind)
            {
                case Model.FilterKind.Date:
                    var range = DateDefaultRange.Resolve(filter.DefaultExpression!, todayUtc);
                    state.SetDate(filter.Name, range.From, range.To);
                    break;
                case Model.FilterKind.Field:
                    state.SetField(filter.Name, []);
                    break;
                case Model.FilterKind.Top:
                    state.SetTop(filter.Name, TopLimitDefaults.Resolve(filter, null));
                    break;
            }
        }

        return state;
    }

    public static IReadOnlyDictionary<string, Model.FilterDefinition> IndexFilters(Model.DashboardDocument document) =>
        document.Filters.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
}
