namespace DashSpec.Core.Runtime;

internal static class PeriodAnchorResolver
{
    public static DateOnly ResolveAnchor(DateOnly selected, string? grain) =>
        grain?.Trim().ToLowerInvariant() switch
        {
            "month" => new DateOnly(selected.Year, selected.Month, 1),
            "year" => new DateOnly(selected.Year, 1, 1),
            _ => selected,
        };

    public static string? TryReadGrain(FilterState filters, string? grainFilterName)
    {
        if (string.IsNullOrWhiteSpace(grainFilterName))
        {
            return null;
        }

        var field = filters.GetField(grainFilterName);
        if (field is null || !field.Value.HasSelection)
        {
            return null;
        }

        return field.Value.Values[0];
    }
}
