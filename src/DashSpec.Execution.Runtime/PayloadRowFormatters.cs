namespace DashSpec.Execution.Runtime;

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

    public static string FormatValue(object? value) => LabelFormat.FormatObject(value);

    public static string FormatHeatmapLabel(object? value) => LabelFormat.FormatObject(value, "date.short");

    public static string FormatHeatmapAxisLabel(object? value, string? format) =>
        LabelFormat.FormatObject(value, format);

    public static string FormatChartAxisLabel(object? value, string? format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            format = value switch
            {
                DateOnly => LabelFormat.ResolveDateFormat(null),
                DateTime => LabelFormat.ResolveTimeFormat(null),
                _ => null,
            };
        }

        return LabelFormat.FormatObject(value, format);
    }

    public static DateOnly? TryParseHeatmapDate(string label) =>
        DateOnly.TryParse(label, System.Globalization.CultureInfo.InvariantCulture, out var date)
            ? date
            : DateOnly.TryParse(label, out date)
                ? date
                : null;

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
