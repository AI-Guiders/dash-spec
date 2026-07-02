namespace DashSpec.Core.Runtime;

internal static class PayloadRowFormatters
{
    public static double? ToDouble(object? value) =>
        value switch
        {
            null => null,
            double d => d,
            float f => f,
            decimal m => (double)m,
            int i => i,
            long l => l,
            _ => double.TryParse(Convert.ToString(value), out var parsed) ? parsed : null,
        };

    public static string FormatValue(object? value) =>
        value switch
        {
            null => string.Empty,
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm"),
            DateOnly d => d.ToString("yyyy-MM-dd"),
            _ => Convert.ToString(value) ?? string.Empty,
        };

    public static string FormatHeatmapLabel(object? value) =>
        value switch
        {
            null => string.Empty,
            DateTime dt => dt.ToString("yyyy-MM-dd"),
            DateOnly d => d.ToString("yyyy-MM-dd"),
            _ => Convert.ToString(value) ?? string.Empty,
        };

    public static string FormatHeatmapAxisLabel(object? value, string? format)
    {
        var raw = value switch
        {
            null => string.Empty,
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd"),
            _ => Convert.ToString(value) ?? string.Empty,
        };
        return string.IsNullOrEmpty(raw) ? raw : LabelFormat.Format(raw, format);
    }

    public static string FormatChartAxisLabel(object? value, string? format)
    {
        var raw = FormatValue(value);
        if (string.IsNullOrEmpty(raw))
        {
            return raw;
        }

        return string.IsNullOrWhiteSpace(format) ? raw : LabelFormat.Format(raw, format);
    }

    public static DateOnly? TryParseHeatmapDate(string label) =>
        DateOnly.TryParse(label, out var date) ? date : null;

    public static string MergeTooltipStrings(string? left, string right, string split)
    {
        var items = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in (left ?? string.Empty).Split(split, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            items.Add(part);
        }

        foreach (var part in right.Split(split, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            items.Add(part);
        }

        return string.Join(split, items.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
    }
}
