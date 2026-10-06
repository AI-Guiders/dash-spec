using System.Globalization;
using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

/// <summary>Human-readable legend for report <c>defaults</c> date/time formats (how to read labels on charts and filters).</summary>
public static class ReportFormatReadingGuide
{
    private static readonly DateOnly SampleDate = new(2026, 8, 3);
    private static readonly DateTime SampleClock = new(2026, 8, 3, 14, 5, 0, DateTimeKind.Unspecified);

    public static ReportFormatReadingGuideModel BuildModel(
        ReportFormatDefaults? defaults,
        TimeZoneInfo? displayTimeZone)
    {
        var dateFormat = ResolveDateFormat(defaults);
        var timeFormat = ResolveTimeFormat(defaults);
        var culture = FormatCulturePresets.ResolveCulture(defaults, null);

        return new ReportFormatReadingGuideModel(
            [
                new ReportFormatGuideRow(
                    "Даты",
                    DescribePattern(dateFormat, isTime: false, culture),
                    FormatSampleDate(dateFormat, culture)),
                new ReportFormatGuideRow(
                    "Время",
                    DescribePattern(timeFormat, isTime: true, culture),
                    FormatSampleTime(timeFormat, culture)),
            ],
            DescribeTimeZone(displayTimeZone));
    }

    public static string Build(ReportFormatDefaults? defaults, TimeZoneInfo? displayTimeZone)
    {
        var model = BuildModel(defaults, displayTimeZone);
        var date = model.Rows[0];
        var time = model.Rows[1];
        return $"Даты: {date.Pattern} (напр. {date.Example}) · Время: {time.Pattern} (напр. {time.Example}) · {model.TimeZoneLabel}";
    }

    internal static string ResolveDateFormat(ReportFormatDefaults? defaults) =>
        string.IsNullOrWhiteSpace(defaults?.DateFormat)
            ? "date.short"
            : defaults.DateFormat.Trim();

    internal static string ResolveTimeFormat(ReportFormatDefaults? defaults) =>
        string.IsNullOrWhiteSpace(defaults?.TimeFormat)
            ? "time.short"
            : defaults.TimeFormat.Trim();

    private static string DescribePattern(string format, bool isTime, CultureInfo culture)
    {
        if (FormatCulturePresets.IsNamedPreset(format))
        {
            var pattern = FormatCulturePresets.ResolvePresetPattern(format, culture);
            return TranslateCustomPattern(pattern, isTime);
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

        if (isTime)
        {
            return translated.Replace("mm", "мм", StringComparison.Ordinal);
        }

        if (translated.Contains('м') && !translated.Contains('Ч'))
        {
            return translated;
        }

        return translated;
    }

    private static string FormatSampleDate(string format, CultureInfo culture)
    {
        if (FormatCulturePresets.IsNamedPreset(format))
        {
            var pattern = FormatCulturePresets.ResolvePresetPattern(format, culture);
            var formatCulture = format.Equals("date.iso", StringComparison.OrdinalIgnoreCase)
                || format.Equals("datetime.iso", StringComparison.OrdinalIgnoreCase)
                    ? CultureInfo.InvariantCulture
                    : culture;
            if (format.StartsWith("datetime", StringComparison.OrdinalIgnoreCase))
            {
                return SampleClock.ToString(pattern, formatCulture);
            }

            return SampleDate.ToString(pattern, formatCulture);
        }

        try
        {
            return SampleDate.ToString(format, culture);
        }
        catch (FormatException)
        {
            return SampleDate.ToString(culture.DateTimeFormat.ShortDatePattern, culture);
        }
    }

    private static string FormatSampleTime(string format, CultureInfo culture)
    {
        if (FormatCulturePresets.IsNamedPreset(format))
        {
            var pattern = FormatCulturePresets.ResolvePresetPattern(format, culture);
            return SampleClock.ToString(pattern, culture);
        }

        try
        {
            return SampleClock.ToString(format, culture);
        }
        catch (FormatException)
        {
            return SampleClock.ToString(culture.DateTimeFormat.ShortTimePattern, culture);
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
