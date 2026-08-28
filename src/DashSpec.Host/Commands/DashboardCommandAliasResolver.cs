#nullable enable
using DashSpec.Core.Model;

namespace DashSpec.Host.Commands;

internal static class DashboardCommandAliasResolver
{
    public static string? ResolveDateFilter(DashboardFilterContext context)
    {
        if (context.CommandAliases.TryGetValue("date", out var aliased) &&
            IsToolbarDateFilter(context, aliased))
        {
            return aliased;
        }

        return context.ToolbarFilterNames
            .FirstOrDefault(name =>
                context.FilterIndex.TryGetValue(name, out var filter) &&
                filter.Kind is FilterKind.Date);
    }

    public static string? ResolveFieldFilter(string slashAlias, DashboardFilterContext context)
    {
        if (context.CommandAliases.TryGetValue(slashAlias, out var aliased) &&
            IsToolbarFieldFilter(context, aliased))
        {
            return aliased;
        }

        if (IsToolbarFieldFilter(context, slashAlias))
        {
            return slashAlias;
        }

        return null;
    }

    public static IReadOnlyList<string> ResolveFieldSlashAliases(
        DashboardFilterContext context)
    {
        var aliases = new List<string>();
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (alias, filterId) in context.CommandAliases)
        {
            if (string.Equals(alias, "date", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsToolbarFieldFilter(context, filterId))
            {
                continue;
            }

            aliases.Add(alias);
            claimed.Add(filterId);
        }

        foreach (var filterName in context.ToolbarFilterNames)
        {
            if (!context.FilterIndex.TryGetValue(filterName, out var filter) ||
                filter.Kind is not FilterKind.Field)
            {
                continue;
            }

            if (claimed.Contains(filter.Name))
            {
                continue;
            }

            aliases.Add(filter.Name);
        }

        return aliases
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    static bool IsToolbarDateFilter(DashboardFilterContext context, string filterName) =>
        context.ToolbarFilterNames.Contains(filterName, StringComparer.OrdinalIgnoreCase) &&
        context.FilterIndex.TryGetValue(filterName, out var filter) &&
        filter.Kind is FilterKind.Date;

    static bool IsToolbarFieldFilter(DashboardFilterContext context, string filterName) =>
        context.ToolbarFilterNames.Contains(filterName, StringComparer.OrdinalIgnoreCase) &&
        context.FilterIndex.TryGetValue(filterName, out var filter) &&
        filter.Kind is FilterKind.Field;
}
