using System.Globalization;
using System.Text.RegularExpressions;
using DashSpec.Abstractions.Data;
using DashSpec.Core.Localization;
using DashSpec.Core.Model;
using DashSpec.Core.Runtime;

namespace DashSpec.Execution.Runtime;

/// <summary>SSOT for display: presets, report defaults, TZ conversion. Parsing raw strings delegates to Core <see cref="DateValueCodec"/>.</summary>
public static partial class LabelFormat
{
    private static readonly AsyncLocal<ReportFormatDefaults?> ReportDefaults = new();
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

    /// <summary>Effective BCL culture for the active report (defaults.culture → host UI → <see cref="CultureInfo.CurrentCulture"/>).</summary>
    public static CultureInfo ResolveReportCulture() => ReportCulture;

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

    /// <summary>Parse a display label or raw value for chronological ordering (heatmap / axis sort).</summary>
    public static DateTime? TryParseChronological(object? raw, string? resolvedFormat)
    {
        if (raw is DateOnly day)
        {
            return day.ToDateTime(TimeOnly.MinValue);
        }

        if (raw is DashDisplayDateTime display)
        {
            return display.Civil;
        }

        if (raw is DateTime dt)
        {
            return ToDisplayTime(dt);
        }

        if (raw is DateTimeOffset dto)
        {
            return ToDisplayTime(dto.UtcDateTime);
        }

        var format = string.IsNullOrWhiteSpace(resolvedFormat)
            ? ResolveDateFormat(null)
            : resolvedFormat.Trim();

        var text = Convert.ToString(raw)?.Trim();
        if (!string.IsNullOrWhiteSpace(text)
            && TryParseDisplayLabel(text, format) is DateTime fromDisplay)
        {
            return fromDisplay;
        }

        if (TimeSeriesGrid.TryParseBucket(raw) is DateTime bucket)
        {
            return ToDisplayTime(bucket);
        }

        return null;
    }

    /// <summary>Parse text already formatted for display (C# pattern or named preset).</summary>
    public static DateTime? TryParseDisplayLabel(string? text, string? resolvedFormat)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var label = text.Trim();
        var format = string.IsNullOrWhiteSpace(resolvedFormat)
            ? ResolveDateFormat(null)
            : resolvedFormat.Trim();

        if (format.Equals("time.short", StringComparison.OrdinalIgnoreCase)
            && TryParseTimeLabel(label, out var time))
        {
            return DateTime.Today.Add(time.ToTimeSpan());
        }

        if (FormatCulturePresets.IsNamedPreset(format))
        {
            var culture = ReportCulture;
            var pattern = FormatCulturePresets.ResolvePresetPattern(format, culture);
            if (format.Equals("datetime.short", StringComparison.OrdinalIgnoreCase)
                || format.Equals("datetime.iso", StringComparison.OrdinalIgnoreCase))
            {
                if (DateTime.TryParseExact(
                        label,
                        pattern,
                        format.Equals("datetime.iso", StringComparison.OrdinalIgnoreCase)
                            ? CultureInfo.InvariantCulture
                            : culture,
                        DateTimeStyles.None,
                        out var shortDateTime))
                {
                    return shortDateTime;
                }
            }
            else if (DateOnly.TryParseExact(
                         label,
                         pattern,
                         format.Equals("date.iso", StringComparison.OrdinalIgnoreCase)
                             ? CultureInfo.InvariantCulture
                             : culture,
                         DateTimeStyles.None,
                         out var presetDay))
            {
                return presetDay.ToDateTime(TimeOnly.MinValue);
            }
        }
        else if (TryParseExactDisplay(label, format, out var custom))
        {
            return custom;
        }

        if (DateValueCodec.TryParseStoredDateOnly(label, out var wireDay))
        {
            return wireDay.ToDateTime(TimeOnly.MinValue);
        }

        if (DateValueCodec.TryParseStoredDateTime(label, out var wireDateTime))
        {
            return ToDisplayTime(wireDateTime);
        }

        if (TryParseTimeLabel(label, out var fallbackTime))
        {
            return DateTime.Today.Add(fallbackTime.ToTimeSpan());
        }

        var fallbackCulture = ReportCulture;
        var shortDatePattern = FormatCulturePresets.ResolvePresetPattern("date.short", fallbackCulture);
        if (DateOnly.TryParseExact(label, shortDatePattern, fallbackCulture, DateTimeStyles.None, out var fallbackDay))
        {
            return fallbackDay.ToDateTime(TimeOnly.MinValue);
        }

        var shortDateTimePattern = FormatCulturePresets.ResolvePresetPattern("datetime.short", fallbackCulture);
        if (DateTime.TryParseExact(label, shortDateTimePattern, fallbackCulture, DateTimeStyles.None, out var fallbackDateTime))
        {
            return fallbackDateTime;
        }

        return null;
    }

    public static DateTime ChronologicalSortKey(object? raw, string? diagramFormat) =>
        TryParseChronological(raw, ResolveAxisFormat(diagramFormat)) ?? DateTime.MaxValue;

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
            DashDisplayDateTime display => FormatDisplayDateTime(
                display.Civil,
                string.IsNullOrWhiteSpace(format) ? ResolveDefaultForDateTime(display.Civil) : format.Trim()),
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

        if (!normalized.Equals("raw", StringComparison.OrdinalIgnoreCase)
            && !normalized.Equals("user.short", StringComparison.OrdinalIgnoreCase)
            && !normalized.StartsWith("truncate.", StringComparison.OrdinalIgnoreCase)
            && TryParseDisplayLabel(raw.Trim(), normalized) is DateTime displayParsed)
        {
            return FormatContainsClock(normalized)
                ? FormatDisplayDateTime(displayParsed, normalized)
                : FormatDateOnly(DateOnly.FromDateTime(displayParsed), normalized);
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

        if (DisplayTimeZone is null)
        {
            return dt.Kind == DateTimeKind.Unspecified
                ? dt
                : DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
        }

        var utc = dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Unspecified => DateValueCodec.NormalizeStorageUtc(dt),
            _ => DateTime.SpecifyKind(dt.ToUniversalTime(), DateTimeKind.Utc),
        };
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

        if (FormatCulturePresets.IsNamedPreset(format))
        {
            var culture = ReportCulture;
            var pattern = FormatCulturePresets.ResolvePresetPattern(format, culture);
            var formatCulture = format.Equals("date.iso", StringComparison.OrdinalIgnoreCase)
                || format.Equals("datetime.iso", StringComparison.OrdinalIgnoreCase)
                    ? CultureInfo.InvariantCulture
                    : culture;
            return display.ToString(pattern, formatCulture);
        }

        if (IsNamedPreset(format))
        {
            return display.ToString("G", ResolveCulture(forSystem: true));
        }

        return ApplyCustomFormat(display, format);
    }

    private static string FormatDateOnly(DateOnly date, string format)
    {
        if (format.Equals("system", StringComparison.OrdinalIgnoreCase))
        {
            return date.ToString("d", ResolveCulture(forSystem: true));
        }

        if (FormatCulturePresets.IsNamedPreset(format))
        {
            var culture = ReportCulture;
            var pattern = FormatCulturePresets.ResolvePresetPattern(format, culture);
            var formatCulture = format.Equals("date.iso", StringComparison.OrdinalIgnoreCase)
                || format.Equals("datetime.iso", StringComparison.OrdinalIgnoreCase)
                    ? CultureInfo.InvariantCulture
                    : culture;
            if (format.Equals("datetime.short", StringComparison.OrdinalIgnoreCase)
                || format.Equals("time.short", StringComparison.OrdinalIgnoreCase))
            {
                return date.ToString(FormatCulturePresets.ResolvePresetPattern("date.short", culture), culture);
            }

            return date.ToString(pattern, formatCulture);
        }

        if (IsNamedPreset(format))
        {
            return date.ToString("d", ResolveCulture(forSystem: true));
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

    private static CultureInfo ReportCulture =>
        FormatCulturePresets.ResolveCulture(ReportDefaults.Value, UiCulture);

    private static CultureInfo ResolveCulture(bool forSystem) =>
        forSystem
            ? UiCulture ?? CultureInfo.CurrentCulture
            : ReportCulture;

    private static string FormatScalar(object value, string? format)
    {
        var text = Convert.ToString(value, ReportCulture) ?? string.Empty;
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

    private static bool TryParseDateTime(string raw, out DateTime dt) =>
        DateValueCodec.TryParseStoredDateTime(raw, out dt);

    private static bool TryParseDateOnly(string raw, out DateOnly date) =>
        DateValueCodec.TryParseStoredDateOnly(raw, out date);

    private static bool TryParseTimeLabel(string text, out TimeOnly time) =>
        TimeOnly.TryParse(text, ReportCulture, DateTimeStyles.None, out time)
        || TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    private static bool FormatContainsClock(string format) =>
        format.Contains('H', StringComparison.Ordinal) || format.Contains('h', StringComparison.Ordinal);

    private static bool TryParseExactDisplay(string label, string format, out DateTime result)
    {
        result = default;
        var culture = ResolveCulture(forSystem: false);
        if (DateOnly.TryParseExact(label, format, culture, DateTimeStyles.None, out var day))
        {
            result = day.ToDateTime(TimeOnly.MinValue);
            return true;
        }

        return DateTime.TryParseExact(label, format, culture, DateTimeStyles.None, out result);
    }

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
