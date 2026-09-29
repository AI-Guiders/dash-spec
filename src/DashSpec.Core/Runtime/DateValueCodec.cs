using System.Globalization;
using System.Text.RegularExpressions;

namespace DashSpec.Core.Runtime;

/// <summary>
/// SSOT for parsing date/time values: wire tokens, spec defaults, SQL cell payloads.
/// Display formatting lives in Execution.Runtime LabelFormat.
/// Filter preset grammar (today, weeks, locale slash input) composes this type in Host <c>DateFilterPresets</c>.
/// </summary>
public static partial class DateValueCodec
{
    public const string WireDayPattern = "yyyy-MM-dd";

    public static readonly string[] StoredDateTimePatterns =
    [
        WireDayPattern,
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.FFFFFFF",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
        "O",
    ];

    private static readonly DateTimeStyles StoredDateTimeStyles =
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;

    public static string FormatWireDay(DateOnly day) =>
        day.ToString(WireDayPattern, CultureInfo.InvariantCulture);

    public static bool TryParseWireDay(string? raw, out DateOnly day)
    {
        day = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return DateOnly.TryParseExact(
            raw.Trim(),
            WireDayPattern,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out day);
    }

    public static bool TryParseWireMonthAnchor(string? yyyyMm, out DateOnly firstDay)
    {
        firstDay = default;
        return !string.IsNullOrWhiteSpace(yyyyMm)
               && TryParseWireDay($"{yyyyMm.Trim()}-01", out firstDay);
    }

    public static bool TryParseStoredDateTime(string? raw, out DateTime utc)
    {
        utc = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim();
        if (DateTime.TryParseExact(
                text,
                StoredDateTimePatterns,
                CultureInfo.InvariantCulture,
                StoredDateTimeStyles,
                out utc))
        {
            return true;
        }

        return DateTime.TryParse(
            text,
            CultureInfo.InvariantCulture,
            StoredDateTimeStyles,
            out utc);
    }

    /// <summary>SQL <c>datetime2</c> and legacy readers often return <see cref="DateTimeKind.Unspecified"/> for UTC columns.</summary>
    public static DateTime NormalizeStorageUtc(DateTime dt) =>
        dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
        };

    public static DateTime? TryParseStoredBucket(object? value) =>
        value switch
        {
            null => null,
            DateTime dt => NormalizeStorageUtc(dt),
            DateTimeOffset dto => dto.UtcDateTime,
            DateOnly d => d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
            _ => TryParseStoredDateTime(Convert.ToString(value), out var parsed) ? parsed : null,
        };

    public static bool TryParseStoredDateOnly(string? raw, out DateOnly day) =>
        TryParseWireDay(raw, out day);

    /// <summary>Heatmap/chart axis labels: wire day, <c>dd.MM</c> (year from anchor), <c>dd.MM.yyyy</c>.</summary>
    public static bool TryParseChartAxisDayLabel(string? raw, int anchorYear, out DateOnly day)
    {
        day = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim();
        if (TryParseWireDay(text, out day))
        {
            return true;
        }

        if (DateOnly.TryParseExact(text, "dd.MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var shortDay)
            && TryCreateCalendarDate(anchorYear, shortDay.Month, shortDay.Day, out day))
        {
            return true;
        }

        if (DateOnly.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
        {
            return true;
        }

        return TryParseDashedDayMonthYear(text, out day);
    }

    /// <summary>Legacy wire/display typo: day-month-year with dashes (e.g. 03-08-2026).</summary>
    public static bool TryParseDashedDayMonthYear(string token, out DateOnly day)
    {
        day = default;
        var misordered = DashedDayMonthYearPattern().Match(token);
        if (!misordered.Success
            || !int.TryParse(misordered.Groups["year"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(misordered.Groups["month"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || !int.TryParse(misordered.Groups["day"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var dayOfMonth)
            || !TryCreateCalendarDate(year, month, dayOfMonth, out day))
        {
            return false;
        }

        return true;
    }

    public static bool TryCreateCalendarDate(int year, int month, int day, out DateOnly value)
    {
        value = default;
        if (year is < 1 or > 9999 || month is < 1 or > 12)
        {
            return false;
        }

        var daysInMonth = DateTime.DaysInMonth(year, month);
        if (day < 1 || day > daysInMonth)
        {
            return false;
        }

        value = new DateOnly(year, month, day);
        return true;
    }

    [GeneratedRegex(@"^(?<day>\d{1,4})-(?<month>\d{1,2})-(?<year>\d{4})$")]
    private static partial Regex DashedDayMonthYearPattern();
}
