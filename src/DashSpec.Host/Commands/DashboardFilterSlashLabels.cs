#nullable enable

namespace DashSpec.Host.Commands;

internal static class DashboardFilterSlashLabels
{
    public static string ResolveFieldLabel(DashboardFilterContext context, string slashAlias)
    {
        var filterName = DashboardCommandAliasResolver.ResolveFieldFilter(slashAlias, context);
        if (filterName is not null && context.FilterIndex.TryGetValue(filterName, out var filter))
        {
            return FirstNonEmpty(filter.Label, filter.Name, slashAlias);
        }

        return slashAlias;
    }

    public static string ResolveDateLabel(DashboardFilterContext context)
    {
        var filterName = DashboardCommandAliasResolver.ResolveDateFilter(context);
        if (filterName is not null && context.FilterIndex.TryGetValue(filterName, out var filter))
        {
            return FirstNonEmpty(filter.Label, filter.Name, "date");
        }

        return "date";
    }

    public static string DateCommandHelp(DashboardFilterContext context) =>
        $"{ResolveDateLabel(context)} — today, last-week, YYYY-MM, range";

    public static string FieldCommandHelp(DashboardFilterContext context, string slashAlias) =>
        $"{ResolveFieldLabel(context, slashAlias)} — значение, all или [a, b]";

    static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "";
    }
}
