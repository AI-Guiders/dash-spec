namespace DashSpec.Core.Model;

public enum CategorySortMode
{
    Value,
    Label,
}

public enum CategoryAxis
{
    X,
    Y,
}

/// <summary>Category axis sort policy from diagram properties (ADR-0062).</summary>
public readonly record struct CategoryAxisOrder(CategorySortMode SortMode, bool Ascending);

public static class CategoryAxisOrderParser
{
    public static CategoryAxisOrder Parse(
        IReadOnlyDictionary<string, string> diagramProperties,
        CategoryAxis axis)
    {
        var sortMode = ResolveSortMode(diagramProperties, axis);
        var ascending = ResolveAscending(sortMode, diagramProperties, axis);
        return new CategoryAxisOrder(sortMode, ascending);
    }

    public static CategorySortMode ResolveSortMode(
        IReadOnlyDictionary<string, string> diagramProperties,
        CategoryAxis axis)
    {
        var sortKey = SortProperty(axis);
        if (diagramProperties.TryGetValue(sortKey, out var raw) &&
            !string.IsNullOrWhiteSpace(raw))
        {
            return string.Equals(raw.Trim(), "label", StringComparison.OrdinalIgnoreCase)
                ? CategorySortMode.Label
                : CategorySortMode.Value;
        }

        diagramProperties.TryGetValue(FormatProperty(axis), out var format);
        return InferSortModeFromFormat(format);
    }

    public static bool ResolveAscending(
        CategorySortMode sortMode,
        IReadOnlyDictionary<string, string> diagramProperties,
        CategoryAxis axis)
    {
        var orderKey = OrderProperty(axis);
        if (diagramProperties.TryGetValue(orderKey, out var raw) &&
            !string.IsNullOrWhiteSpace(raw))
        {
            return string.Equals(raw.Trim(), "asc", StringComparison.OrdinalIgnoreCase);
        }

        return sortMode is CategorySortMode.Label;
    }

    public static CategorySortMode InferSortModeFromFormat(string? format)
    {
        var effective = string.IsNullOrWhiteSpace(format) ? "user.short" : format.Trim();
        if (string.Equals(effective, "raw", StringComparison.OrdinalIgnoreCase))
        {
            return CategorySortMode.Label;
        }

        var normalized = effective.ToLowerInvariant();
        if (normalized.StartsWith("user.", StringComparison.Ordinal) ||
            normalized.StartsWith("date.", StringComparison.Ordinal) ||
            normalized.StartsWith("time.", StringComparison.Ordinal) ||
            normalized is "number" or "numeric")
        {
            return CategorySortMode.Value;
        }

        return CategorySortMode.Label;
    }

    private static string SortProperty(CategoryAxis axis) =>
        axis is CategoryAxis.Y ? "y_sort" : "x_sort";

    private static string OrderProperty(CategoryAxis axis) =>
        axis is CategoryAxis.Y ? "y_order" : "x_order";

    private static string FormatProperty(CategoryAxis axis) =>
        axis is CategoryAxis.Y ? "y_format" : "x_format";
}

public static class CategoryAxisOrdering
{
    public static void Sort(
        List<string> categoryLabels,
        IReadOnlyDictionary<string, double> categoryTotals,
        CategoryAxisOrder order)
    {
        ArgumentNullException.ThrowIfNull(categoryLabels);
        ArgumentNullException.ThrowIfNull(categoryTotals);

        categoryLabels.Sort((a, b) =>
        {
            var cmp = order.SortMode is CategorySortMode.Label
                ? string.Compare(a, b, StringComparison.OrdinalIgnoreCase)
                : categoryTotals.GetValueOrDefault(a).CompareTo(categoryTotals.GetValueOrDefault(b));
            return order.Ascending ? cmp : -cmp;
        });
    }

    public static void Sort(
        List<string> categoryLabels,
        IReadOnlyDictionary<string, double> categoryTotals,
        IReadOnlyDictionary<string, string> diagramProperties,
        CategoryAxis axis = CategoryAxis.Y)
    {
        var order = CategoryAxisOrderParser.Parse(diagramProperties, axis);
        Sort(categoryLabels, categoryTotals, order);
    }
}
