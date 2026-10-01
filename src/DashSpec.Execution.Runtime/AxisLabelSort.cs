namespace DashSpec.Execution.Runtime;

/// <summary>Chronological ordering for formatted chart/matrix axis labels.</summary>
internal static class AxisLabelSort
{
    public static DateTime ResolveSortKey(object? raw, string? axisFormat) =>
        LabelFormat.ChronologicalSortKey(raw, axisFormat);

    public static int CompareFormattedLabels(string? left, string? right, string? axisFormat)
    {
        var resolved = LabelFormat.ResolveAxisFormat(axisFormat);
        var a = LabelFormat.TryParseDisplayLabel(left, resolved);
        var b = LabelFormat.TryParseDisplayLabel(right, resolved);
        if (a.HasValue && b.HasValue)
        {
            return a.Value.CompareTo(b.Value);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
