#nullable enable
using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Host.Commands;

internal static class DashboardFilterSlashLabels
{
    public static string ResolveFilterLabel(DashboardFilterContext context, string filterName)
    {
        if (context.FilterIndex.TryGetValue(filterName, out var filter))
        {
            return FirstNonEmpty(filter.Label, filter.Name, filterName);
        }

        return filterName;
    }

    public static string DateFilterHelp(DashboardFilterContext context, string filterName) =>
        DateFilterHint(context, filterName);

    public static string DateFilterHint(DashboardFilterContext context, string filterName)
    {
        var label = ResolveFilterLabel(context, filterName);
        if (context.FilterIndex.TryGetValue(filterName, out var filter)
            && !string.IsNullOrWhiteSpace(filter.GrainFilterName))
        {
            var grain = GrainFilterPresentation.ResolveGrain(
                filter,
                context.FilterIndex,
                context.UiState.SelectedFields);
            var grainHint = grain switch
            {
                "month" => "YYYY-MM, today",
                "year" => "YYYY, today",
                _ => "today, last-week, YYYY-MM-DD, from..to",
            };
            return $"{label} — {grainHint}";
        }

        return $"{label} — today, last-week, YYYY-MM, from..to";
    }

    public static string FieldFilterHelp(DashboardFilterContext context, string filterName) =>
        FieldFilterHint(context, filterName);

    public static string FieldFilterHint(DashboardFilterContext context, string filterName) =>
        $"{ResolveFilterLabel(context, filterName)} — значение, all или [a, b]";

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
