using System.Globalization;
using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

/// <summary>Human-readable legend for report <c>defaults</c> date/time formats (how to read labels on charts and filters).</summary>
public static class ReportFormatReadingGuide
{
    private static readonly CultureInfo SampleCulture = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly DateOnly SampleDate = new(2026, 8, 3);
    private static readonly DateTime SampleClock = new(2026, 8, 3, 14, 5, 0, DateTimeKind.Unspecified);

    public static string Build(ReportFormatDefaults? defaults, TimeZoneInfo? displayTimeZone)
    {
        var dateFormat = ResolveDateFormat(defaults);
        var timeFormat = ResolveTimeFormat(defaults);
        var datePattern = DescribePattern(dateFormat, isTime: false);
        var timePattern = DescribePattern(timeFormat, isTime: true);
        var dateExample = FormatSampleDate(dateFormat);
        var timeExample = FormatSampleTime(timeFormat);
        var zone = DescribeTimeZone(displayTimeZone);

        return $"Даты: {datePattern} (напр. {dateExample}) · Время: {timePattern} (напр. {timeExample}) · {zone}";
    }

    internal static string ResolveDateFormat(ReportFormatDefaults? defaults) =>
        string.IsNullOrWhiteSpace(defaults?.DateFormat)
            ? "date.short"
            : defaults.DateFormat.Trim();

    internal static string ResolveTimeFormat(ReportFormatDefaults? defaults) =>
        string.IsNullOrWhiteSpace(defaults?.TimeFormat)
            ? "time.short"
            : defaults.TimeFormat.Trim();

    private static string DescribePattern(string format, bool isTime)
    {
        if (format.Equals("date.short", StringComparison.OrdinalIgnoreCase))
        {
            return "дд.мм";
        }

        if (format.Equals("date.full", StringComparison.OrdinalIgnoreCase))
        {
            return "дд.мм.гггг";
        }

        if (format.Equals("date.iso", StringComparison.OrdinalIgnoreCase))
        {
            return "гггг-мм-дд";
        }

        if (format.Equals("time.short", StringComparison.OrdinalIgnoreCase))
        {
            return "ЧЧ:мм";
        }

        if (format.Equals("datetime.short", StringComparison.OrdinalIgnoreCase))
        {
            return "дд.мм ЧЧ:мм";
        }

        if (format.Equals("datetime.iso", StringComparison.OrdinalIgnoreCase))
        {
            return "гггг-мм-дд ЧЧ:мм";
        }

        return TranslateCustomPattern(format, isTime);
    }

    private static string TranslateCustomPattern(string format, bool isTime)
    {
        var translated = format
            .Replace("yyyy", "гггг", StringComparison.Ordinal)
            .Replace("dd", "дд", StringComparison.Ordinal)
            .Replace("MM", "мм", StringComparison.Ordinal)
            .Replace("HH", "ЧЧ", StringComparison.Ordinal);

        if (!isTime && translated.Contains('м') && !translated.Contains('Ч'))
        {
            return translated;
        }

        return translated;
    }

    private static string FormatSampleDate(string format)
    {
        if (format.Equals("date.short", StringComparison.OrdinalIgnoreCase))
        {
            return SampleDate.ToString("dd.MM", SampleCulture);
        }

        if (format.Equals("date.full", StringComparison.OrdinalIgnoreCase))
        {
            return SampleDate.ToString("dd.MM.yyyy", SampleCulture);
        }

        if (format.Equals("date.iso", StringComparison.OrdinalIgnoreCase))
        {
            return SampleDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (format.Equals("datetime.short", StringComparison.OrdinalIgnoreCase))
        {
            return SampleClock.ToString("dd.MM HH:mm", SampleCulture);
        }

        if (format.Equals("datetime.iso", StringComparison.OrdinalIgnoreCase))
        {
            return SampleClock.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        try
        {
            return SampleDate.ToString(format, SampleCulture);
        }
        catch (FormatException)
        {
            return SampleDate.ToString("dd.MM.yyyy", SampleCulture);
        }
    }

    private static string FormatSampleTime(string format)
    {
        if (format.Equals("time.short", StringComparison.OrdinalIgnoreCase))
        {
            return SampleClock.ToString("HH:mm", SampleCulture);
        }

        try
        {
            return SampleClock.ToString(format, SampleCulture);
        }
        catch (FormatException)
        {
            return SampleClock.ToString("HH:mm", SampleCulture);
        }
    }

    private static string DescribeTimeZone(TimeZoneInfo? displayTimeZone)
    {
        if (displayTimeZone is null)
        {
            return "UTC";
        }

        if (displayTimeZone.Id.Contains("Moscow", StringComparison.OrdinalIgnoreCase)
            || displayTimeZone.Id.Equals("Russian Standard Time", StringComparison.OrdinalIgnoreCase)
            || displayTimeZone.Id.Equals("Europe/Moscow", StringComparison.OrdinalIgnoreCase))
        {
            return "часовой пояс МСК";
        }

        var offset = displayTimeZone.GetUtcOffset(DateTime.UtcNow);
        var sign = offset < TimeSpan.Zero ? "−" : "+";
        var hours = Math.Abs(offset.Hours);
        var minutes = Math.Abs(offset.Minutes);
        var offsetText = minutes == 0
            ? $"UTC{sign}{hours}"
            : $"UTC{sign}{hours}:{minutes:00}";
        return $"часовой пояс {displayTimeZone.Id} ({offsetText})";
    }
}
