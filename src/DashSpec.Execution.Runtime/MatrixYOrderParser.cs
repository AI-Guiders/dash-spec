namespace DashSpec.Execution.Runtime;

internal static class MatrixYOrderParser
{
    public static bool IsAscending(IReadOnlyDictionary<string, string> diagramProperties) =>
        diagramProperties.TryGetValue("y_order", out var raw) &&
        string.Equals(raw.Trim(), "asc", StringComparison.OrdinalIgnoreCase);

    public static bool SortByLabel(IReadOnlyDictionary<string, string> diagramProperties) =>
        diagramProperties.TryGetValue("y_sort", out var raw) &&
        string.Equals(raw.Trim(), "label", StringComparison.OrdinalIgnoreCase);

    public static void SortYLabels(
        List<string> yLabels,
        Dictionary<string, double> yTotals,
        IReadOnlyDictionary<string, string> diagramProperties)
    {
        var ascending = IsAscending(diagramProperties);
        var byLabel = SortByLabel(diagramProperties);
        yLabels.Sort((a, b) =>
        {
            var cmp = byLabel
                ? string.Compare(a, b, StringComparison.OrdinalIgnoreCase)
                : yTotals.GetValueOrDefault(a).CompareTo(yTotals.GetValueOrDefault(b));
            return ascending ? cmp : -cmp;
        });
    }
}
