namespace DashSpec.Execution.Runtime;

public enum MatrixYSortMode
{
    Value,
    Label,
}

public static class MatrixYOrderParser
{
    public static MatrixYSortMode ResolveSortMode(IReadOnlyDictionary<string, string> diagramProperties)
    {
        if (diagramProperties.TryGetValue("y_sort", out var raw) &&
            !string.IsNullOrWhiteSpace(raw))
        {
            return string.Equals(raw.Trim(), "label", StringComparison.OrdinalIgnoreCase)
                ? MatrixYSortMode.Label
                : MatrixYSortMode.Value;
        }

        diagramProperties.TryGetValue("y_format", out var yFormat);
        return InferSortModeFromYFormat(yFormat);
    }

    public static bool ResolveAscending(
        MatrixYSortMode sortMode,
        IReadOnlyDictionary<string, string> diagramProperties)
    {
        if (diagramProperties.TryGetValue("y_order", out var raw) &&
            !string.IsNullOrWhiteSpace(raw))
        {
            return string.Equals(raw.Trim(), "asc", StringComparison.OrdinalIgnoreCase);
        }

        return sortMode is MatrixYSortMode.Label;
    }

    public static void SortYLabels(
        List<string> yLabels,
        Dictionary<string, double> yTotals,
        IReadOnlyDictionary<string, string> diagramProperties)
    {
        var sortMode = ResolveSortMode(diagramProperties);
        var ascending = ResolveAscending(sortMode, diagramProperties);
        yLabels.Sort((a, b) =>
        {
            var cmp = sortMode is MatrixYSortMode.Label
                ? string.Compare(a, b, StringComparison.OrdinalIgnoreCase)
                : yTotals.GetValueOrDefault(a).CompareTo(yTotals.GetValueOrDefault(b));
            return ascending ? cmp : -cmp;
        });
    }

    internal static MatrixYSortMode InferSortModeFromYFormat(string? yFormat)
    {
        var effective = string.IsNullOrWhiteSpace(yFormat) ? "user.short" : yFormat.Trim();
        if (string.Equals(effective, "raw", StringComparison.OrdinalIgnoreCase))
        {
            return MatrixYSortMode.Label;
        }

        var normalized = effective.ToLowerInvariant();
        if (normalized.StartsWith("user.", StringComparison.Ordinal) ||
            normalized.StartsWith("date.", StringComparison.Ordinal) ||
            normalized.StartsWith("time.", StringComparison.Ordinal) ||
            normalized is "number" or "numeric")
        {
            return MatrixYSortMode.Value;
        }

        return MatrixYSortMode.Label;
    }
}
