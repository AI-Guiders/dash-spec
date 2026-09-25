using System.Globalization;
using DashSpec.Core.Runtime;

namespace DashSpec.Execution.Runtime;

/// <summary>Chronological ordering for formatted chart/matrix axis labels.</summary>
internal static class AxisLabelSort
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("ru-RU");

    public static DateTime ResolveSortKey(object? raw, string? axisFormat)
    {
        if (TimeSeriesGrid.TryParseBucket(raw) is DateTime dt)
        {
            return dt;
        }

        if (raw is DateOnly day)
        {
            return day.ToDateTime(TimeOnly.MinValue);
        }

        var text = Convert.ToString(raw);
        if (!string.IsNullOrWhiteSpace(text)
            && TryParseFormattedLabel(text.Trim(), axisFormat) is DateTime parsed)
        {
            return parsed;
        }

        return DateTime.MaxValue;
    }

    public static int CompareFormattedLabels(string? left, string? right, string? axisFormat)
    {
        var a = TryParseFormattedLabel(left, axisFormat);
        var b = TryParseFormattedLabel(right, axisFormat);
        if (a.HasValue && b.HasValue)
        {
            return a.Value.CompareTo(b.Value);
        }

        return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? TryParseFormattedLabel(string? label, string? axisFormat)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return null;
        }

        var text = label.Trim();
        var format = axisFormat?.Trim();

        if (string.Equals(format, "time.short", StringComparison.OrdinalIgnoreCase)
            && TryParseTimeLabel(text, out var time))
        {
            return DateTime.Today.Add(time.ToTimeSpan());
        }

        if (string.Equals(format, "date.short", StringComparison.OrdinalIgnoreCase)
            && DateOnly.TryParseExact(text, "dd.MM", DisplayCulture, DateTimeStyles.None, out var shortDay))
        {
            return shortDay.ToDateTime(TimeOnly.MinValue);
        }

        if (string.Equals(format, "date.full", StringComparison.OrdinalIgnoreCase)
            && DateOnly.TryParseExact(text, "dd.MM.yyyy", DisplayCulture, DateTimeStyles.None, out var fullDay))
        {
            return fullDay.ToDateTime(TimeOnly.MinValue);
        }

        if (string.Equals(format, "datetime.short", StringComparison.OrdinalIgnoreCase)
            && DateTime.TryParseExact(text, "dd.MM HH:mm", DisplayCulture, DateTimeStyles.None, out var shortDateTime))
        {
            return shortDateTime;
        }

        if (DateValueCodec.TryParseStoredDateOnly(text, out var wireDay))
        {
            return wireDay.ToDateTime(TimeOnly.MinValue);
        }

        if (DateValueCodec.TryParseStoredDateTime(text, out var wireDateTime))
        {
            return wireDateTime;
        }

        if (TryParseTimeLabel(text, out var fallbackTime))
        {
            return DateTime.Today.Add(fallbackTime.ToTimeSpan());
        }

        if (DateOnly.TryParseExact(text, "dd.MM", DisplayCulture, DateTimeStyles.None, out var fallbackDay))
        {
            return fallbackDay.ToDateTime(TimeOnly.MinValue);
        }

        if (DateTime.TryParseExact(text, "dd.MM HH:mm", DisplayCulture, DateTimeStyles.None, out var fallbackDateTime))
        {
            return fallbackDateTime;
        }

        return null;
    }

    private static bool TryParseTimeLabel(string text, out TimeOnly time) =>
        TimeOnly.TryParse(text, DisplayCulture, DateTimeStyles.None, out time)
        || TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
}
