using System.Globalization;

namespace DashSpec.Core.Runtime;

public static class LabelFormat
{
    public static string Format(string raw, string? format)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return (format ?? "raw").Trim().ToLowerInvariant() switch
        {
            "date.short" => FormatDateShort(raw),
            "date.iso" => FormatDateIso(raw),
            "time.short" => FormatTimeShort(raw),
            "datetime.short" => FormatDateTimeShort(raw),
            "user.short" => FormatUserShort(raw),
            "truncate.22" => Truncate(raw, 22),
            "raw" => raw,
            _ => raw,
        };
    }

    private static string FormatTimeShort(string raw) =>
        TryParseDateTime(raw, out var dt) ? dt.ToString("HH:mm") : raw;

    private static string FormatDateTimeShort(string raw) =>
        TryParseDateTime(raw, out var dt) ? dt.ToString("dd.MM HH:mm") : raw;

    private static bool TryParseDateTime(string raw, out DateTime dt) =>
        DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)
        || DateTime.TryParse(raw, out dt);

    private static string FormatDateShort(string raw) =>
        DateOnly.TryParse(raw, out var date) ? date.ToString("dd.MM") : raw;

    private static string FormatDateIso(string raw) =>
        DateOnly.TryParse(raw, out var date) ? date.ToString("yyyy-MM-dd") : raw;

    private static string FormatUserShort(string raw)
    {
        var slash = raw.LastIndexOf('\\');
        var shortName = slash >= 0 ? raw[(slash + 1)..] : raw;
        return Truncate(shortName, 22);
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
}
