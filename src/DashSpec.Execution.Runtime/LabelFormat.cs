using System.Globalization;
using System.Text.RegularExpressions;
using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

public static partial class LabelFormat
{
    private static readonly AsyncLocal<ReportFormatDefaults?> ReportDefaults = new();
    private static readonly CultureInfo DefaultCulture = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly string[] IsoDateTimePatterns =
    [
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.FFFFFFF",
        "O",
    ];

    private static readonly HashSet<string> NamedPresets = new(StringComparer.OrdinalIgnoreCase)
    {
        "date.short",
        "date.full",
        "date.iso",
        "time.short",
        "datetime.short",
        "datetime.iso",
        "user.short",
        "raw",
        "system",
    };

    /// <summary>Display time zone for output conversion (remark 25: stored UTC → display TZ). Set at host startup.</summary>
    public static TimeZoneInfo? DisplayTimeZone { get; set; }

    /// <summary>UI culture from host localization (ru/en). Used for <c>system</c> and C# format strings.</summary>
    public static CultureInfo? UiCulture { get; set; }

    public static void SetReportDefaults(ReportFormatDefaults? defaults) =>
        ReportDefaults.Value = defaults;

    public static void ClearReportDefaults() =>
        ReportDefaults.Value = null;

    public static string ResolveTimeFormat(string? diagramFormat) =>
        CoalesceFormat(diagramFormat, ReportDefaults.Value?.TimeFormat, "time.short");

    public static string ResolveDateFormat(string? diagramFormat) =>
        CoalesceFormat(diagramFormat, ReportDefaults.Value?.DateFormat, "date.short");

    public static string ResolveDateTimeFormat(string? diagramFormat) =>
        CoalesceFormat(diagramFormat, ReportDefaults.Value?.DateTimeFormat, "datetime.short");

    public static string ResolveAxisFormat(string? diagramFormat)
    {
        if (string.IsNullOrWhiteSpace(diagramFormat))
        {
            return ResolveDateFormat(null);
        }

        var trimmed = diagramFormat.Trim();
        if (trimmed.Equals("time.short", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveTimeFormat(trimmed);
        }

        if (trimmed.Equals("datetime.short", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("datetime.iso", StringComparison.OrdinalIgnoreCase))
        {
            return ResolveDateTimeFormat(trimmed);
        }

        return ResolveDateFormat(trimmed);
    }

    public static bool LooksLikePreformattedTimeLabel(string? raw) =>
        !string.IsNullOrWhiteSpace(raw) && PreformattedTimeLabelPattern().IsMatch(raw.Trim());

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

    /// <summary>
    /// Format a cell/axis value.
    /// <paramref name="format"/> — preset (<c>date.short</c>, <c>system</c>, …) or C# pattern (<c>dd.MM.yyyy HH:mm</c>).
    /// </summary>
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
            DateOnly d => FormatDateOnly(d, format ?? ResolveDateFormat(null)),
            _ => FormatScalar(value, format),
        };
    }

    public static string Format(string raw, string? format)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = (format ?? "raw").Trim();
        if (normalized.Equals("time.short", StringComparison.OrdinalIgnoreCase)
            && LooksLikePreformattedTimeLabel(raw))
        {
            return raw;
        }

        if (TryParseDateTime(raw, out var dt))
        {
            return FormatDisplayDateTime(ToDisplayTime(dt), format ?? ResolveDateTimeFormat(null));
        }

        if (TryParseDateOnly(raw, out var date))
        {
            return FormatDateOnly(date, format ?? ResolveDateFormat(null));
        }

        if (IsNamedPreset(normalized))
        {
            return normalized.ToLowerInvariant() switch
            {
                "user.short" => FormatUserShort(raw),
                "truncate.22" => Truncate(raw, 22),
                "raw" => raw,
                _ => raw,
            };
        }

        return raw;
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
        var display = ToDisplayTime(stored);
        var resolved = string.IsNullOrWhiteSpace(format)
            ? ResolveDefaultForDateTime(display)
            : format.Trim();
        return FormatDisplayDateTime(display, resolved);
    }

    public static string ResolveChartAxisFormat(object? value) =>
        value switch
        {
            DateOnly => ResolveDateFormat(null),
            DateTime dt => ResolveChartAxisDateTimeFormat(ToDisplayTime(dt)),
            DateTimeOffset dto => ResolveChartAxisDateTimeFormat(ToDisplayTime(dto.UtcDateTime)),
            _ => ResolveDateFormat(null),
        };

    private static string ResolveChartAxisDateTimeFormat(DateTime display) =>
        display.TimeOfDay == TimeSpan.Zero
            ? ResolveDateFormat(null)
            : ResolveTimeFormat(null);

    private static string ResolveDefaultForDateTime(DateTime display) =>
        display.TimeOfDay == TimeSpan.Zero
            ? ResolveDateFormat(null)
            : ResolveDateTimeFormat(null);

    private static string CoalesceFormat(string? diagramFormat, string? reportDefault, string fallback) =>
        !string.IsNullOrWhiteSpace(diagramFormat)
            ? diagramFormat.Trim()
            : !string.IsNullOrWhiteSpace(reportDefault)
                ? reportDefault.Trim()
                : fallback;

    private static string FormatDisplayDateTime(DateTime display, string format)
    {
        if (format.Equals("system", StringComparison.OrdinalIgnoreCase))
        {
            return display.ToString("G", ResolveCulture(forSystem: true));
        }

        if (IsNamedPreset(format))
        {
            return format.ToLowerInvariant() switch
            {
                "date.short" => display.ToString("dd.MM", DefaultCulture),
                "date.full" => display.ToString("dd.MM.yyyy", DefaultCulture),
                "date.iso" => display.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                "time.short" => display.ToString("HH:mm", DefaultCulture),
                "datetime.short" => display.ToString("dd.MM HH:mm", DefaultCulture),
                "datetime.iso" => display.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                _ => display.ToString("G", ResolveCulture(forSystem: true)),
            };
        }

        return ApplyCustomFormat(display, format);
    }

    private static string FormatDateOnly(DateOnly date, string format)
    {
        if (format.Equals("system", StringComparison.OrdinalIgnoreCase))
        {
            return date.ToString("d", ResolveCulture(forSystem: true));
        }

        if (IsNamedPreset(format))
        {
            return format.ToLowerInvariant() switch
            {
                "date.short" => date.ToString("dd.MM", DefaultCulture),
                "date.full" => date.ToString("dd.MM.yyyy", DefaultCulture),
                "date.iso" or "datetime.iso" => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                "datetime.short" => date.ToString("dd.MM", DefaultCulture),
                _ => date.ToString("d", ResolveCulture(forSystem: true)),
            };
        }

        return ApplyCustomFormat(date, format);
    }

    private static string ApplyCustomFormat(DateTime display, string format)
    {
        try
        {
            return display.ToString(format, ResolveCulture(forSystem: false));
        }
        catch (FormatException)
        {
            return display.ToString("G", ResolveCulture(forSystem: true));
        }
    }

    private static string ApplyCustomFormat(DateOnly date, string format)
    {
        try
        {
            return date.ToString(format, ResolveCulture(forSystem: false));
        }
        catch (FormatException)
        {
            return date.ToString("d", ResolveCulture(forSystem: true));
        }
    }

    private static CultureInfo ResolveCulture(bool forSystem)
    {
        if (forSystem)
        {
            return UiCulture ?? CultureInfo.CurrentCulture;
        }

        return UiCulture ?? DefaultCulture;
    }

    private static string FormatScalar(object value, string? format)
    {
        var text = Convert.ToString(value, UiCulture ?? DefaultCulture) ?? string.Empty;
        return string.IsNullOrWhiteSpace(format) || format.Equals("raw", StringComparison.OrdinalIgnoreCase)
            ? text
            : Format(text, format);
    }

    private static bool IsNamedPreset(string format)
    {
        if (NamedPresets.Contains(format))
        {
            return true;
        }

        return format.StartsWith("truncate.", StringComparison.OrdinalIgnoreCase);
    }

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

        return false;
    }

    private static bool TryParseDateOnly(string raw, out DateOnly date) =>
        DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
        || DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    [GeneratedRegex(@"^\d{1,2}:\d{2}$")]
    private static partial Regex PreformattedTimeLabelPattern();

    private static string FormatUserShort(string raw)
    {
        var slash = raw.LastIndexOf('\\');
        var shortName = slash >= 0 ? raw[(slash + 1)..] : raw;
        return Truncate(shortName, 22);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}
