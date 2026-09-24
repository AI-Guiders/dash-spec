using System.Globalization;

namespace DashSpec.Execution.Runtime;

public static class LabelFormat
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly string[] IsoDateTimePatterns =
    [
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
        "O",
    ];

    /// <summary>Display time zone for output conversion (remark 25: stored UTC → display TZ). Set at host startup.</summary>
    public static TimeZoneInfo? DisplayTimeZone { get; set; }

    internal static TimeZoneInfo? SafeZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Format a cell/axis value. Author diagram formats (<c>date.short</c>, <c>time.short</c>, …) apply here.</summary>
    public static string FormatObject(object? value, string? format = null)
    {
        if (value is null or DBNull)
        {
            return string.Empty;
        }

        return value switch
        {
            DateTime dt => FormatStoredDateTime(dt, format),
            DateTimeOffset dto => FormatStoredDateTime(dto.UtcDateTime, format),
            DateOnly d => FormatDateOnly(d, format ?? "date.short"),
            _ => FormatScalar(value, format),
        };
    }

    public static string Format(string raw, string? format)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = (format ?? "raw").Trim().ToLowerInvariant();
        if (normalized is "date.short" or "date.iso" or "time.short" or "datetime.short" or "datetime.iso")
        {
            if (TryParseDateTime(raw, out var dt))
            {
                return FormatDisplayDateTime(ToDisplayTime(dt), normalized);
            }

            if (TryParseDateOnly(raw, out var date))
            {
                return FormatDateOnly(date, normalized);
            }
        }

        return normalized switch
        {
            "date.short" => FormatDateShort(raw),
            "date.iso" => FormatDateIso(raw),
            "time.short" => FormatTimeShort(raw),
            "datetime.short" => FormatDateTimeShort(raw),
            "datetime.iso" => FormatDateTimeIso(raw),
            "user.short" => FormatUserShort(raw),
            "truncate.22" => Truncate(raw, 22),
            "raw" => raw,
            _ => raw,
        };
    }

    /// <summary>UTC (or unspecified storage) → wall clock in <see cref="DisplayTimeZone"/>.</summary>
    public static DateTime ToDisplayTime(DateTime dt)
    {
        if (dt == default)
        {
            return dt;
        }

        if (dt.Kind == DateTimeKind.Unspecified)
        {
            return dt;
        }

        if (DisplayTimeZone is null)
        {
            return DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
        }

        var utc = dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, DisplayTimeZone);
    }

    private static string FormatStoredDateTime(DateTime stored, string? format)
    {
        var normalized = string.IsNullOrWhiteSpace(format) ? "datetime.short" : format.Trim().ToLowerInvariant();
        return FormatDisplayDateTime(ToDisplayTime(stored), normalized);
    }

    private static string FormatDisplayDateTime(DateTime display, string format) =>
        format switch
        {
            "date.short" => display.ToString("dd.MM", DisplayCulture),
            "date.full" => display.ToString("dd.MM.yyyy", DisplayCulture),
            "date.iso" => display.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "time.short" => display.ToString("HH:mm", DisplayCulture),
            "datetime.short" => display.ToString("dd.MM HH:mm", DisplayCulture),
            "datetime.iso" => display.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            _ => display.ToString("dd.MM.yyyy HH:mm", DisplayCulture),
        };

    private static string FormatDateOnly(DateOnly date, string format) =>
        format switch
        {
            "date.short" => date.ToString("dd.MM", DisplayCulture),
            "date.full" => date.ToString("dd.MM.yyyy", DisplayCulture),
            "date.iso" or "datetime.iso" => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "datetime.short" => date.ToString("dd.MM", DisplayCulture),
            _ => date.ToString("dd.MM.yyyy", DisplayCulture),
        };

    private static string FormatScalar(object value, string? format)
    {
        var text = Convert.ToString(value, DisplayCulture) ?? string.Empty;
        return string.IsNullOrWhiteSpace(format) || format.Equals("raw", StringComparison.OrdinalIgnoreCase)
            ? text
            : Format(text, format);
    }

    private static string FormatTimeShort(string raw) =>
        TryParseDateTime(raw, out var dt) ? FormatDisplayDateTime(ToDisplayTime(dt), "time.short") : raw;

    private static string FormatDateTimeShort(string raw) =>
        TryParseDateTime(raw, out var dt) ? FormatDisplayDateTime(ToDisplayTime(dt), "datetime.short") : raw;

    private static string FormatDateTimeIso(string raw) =>
        TryParseDateTime(raw, out var dt) ? FormatDisplayDateTime(ToDisplayTime(dt), "datetime.iso") : raw;

    private static bool TryParseDateTime(string raw, out DateTime dt)
    {
        if (DateTime.TryParseExact(
                raw,
                IsoDateTimePatterns,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out dt))
        {
            return true;
        }

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt))
        {
            return true;
        }

        return DateTime.TryParse(raw, DisplayCulture, DateTimeStyles.None, out dt);
    }

    private static bool TryParseDateOnly(string raw, out DateOnly date) =>
        DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
        || DateOnly.TryParse(raw, DisplayCulture, DateTimeStyles.None, out date);

    private static string FormatDateShort(string raw) =>
        TryParseDateOnly(raw, out var date) ? FormatDateOnly(date, "date.short") : raw;

    private static string FormatDateIso(string raw) =>
        TryParseDateOnly(raw, out var date) ? FormatDateOnly(date, "date.iso") : raw;

    private static string FormatUserShort(string raw)
    {
        var slash = raw.LastIndexOf('\\');
        var shortName = slash >= 0 ? raw[(slash + 1)..] : raw;
        return Truncate(shortName, 22);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}
