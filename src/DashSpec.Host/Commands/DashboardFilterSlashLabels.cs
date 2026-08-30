#nullable enable
using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

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

    public static string DateCommandHelp(DashboardFilterContext context)
    {
        var label = ResolveDateLabel(context);
        var filterName = DashboardCommandAliasResolver.ResolveDateFilter(context);
        if (filterName is not null
            && context.FilterIndex.TryGetValue(filterName, out var filter)
            && !string.IsNullOrWhiteSpace(filter.GrainFilterName))
        {
            var grain = GrainFilterPresentation.ResolveGrain(
                filter,
                context.FilterIndex,
                context.UiState.SelectedFields);
            var grainFilterName = filter.GrainFilterName;
            context.FilterIndex.TryGetValue(grainFilterName, out var grainFilter);
            var grainHint = grain switch
            {
                "month" => "YYYY-MM или today",
                "year" => "YYYY или today",
                _ => "today, last-week, YYYY-MM-DD, from..to",
            };
            return $"{label} — {grainHint} (масштаб: {FormatGrain(grain, grainFilter ?? filter)})";
        }

        return $"{label} — today, last-week, YYYY-MM, YYYY-MM-DD..YYYY-MM-DD";
    }

    public static string FieldCommandHelp(DashboardFilterContext context, string slashAlias) =>
        $"{ResolveFieldLabel(context, slashAlias)} — значение, all или [a, b]";

    static string FormatGrain(string? grain, FilterDefinition filter)
    {
        if (grain is not null
            && filter.GrainLabels is not null
            && filter.GrainLabels.TryGetValue(grain, out var label))
        {
            return label;
        }

        return grain switch
        {
            "month" => "месяц",
            "year" => "год",
            _ => "день",
        };
    }

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
