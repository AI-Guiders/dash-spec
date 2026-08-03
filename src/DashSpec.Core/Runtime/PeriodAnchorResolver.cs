namespace DashSpec.Core.Runtime;

public static class PeriodAnchorResolver
{
    public static DateOnly ResolveAnchor(DateOnly selected, string? grain) =>
        grain?.Trim().ToLowerInvariant() switch
        {
            "month" => new DateOnly(selected.Year, selected.Month, 1),
            "year" => new DateOnly(selected.Year, 1, 1),
            _ => selected,
        };

    public static DateOnly ResolvePeriodEnd(DateOnly anchor, string? grain) =>
        grain?.Trim().ToLowerInvariant() switch
        {
            "month" => anchor.AddMonths(1).AddDays(-1),
            "year" => new DateOnly(anchor.Year, 12, 31),
            _ => anchor,
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
