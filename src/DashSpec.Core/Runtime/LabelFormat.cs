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
            "user.short" => FormatUserShort(raw),
            "truncate.22" => Truncate(raw, 22),
            "raw" => raw,
            _ => raw,
        };
    }

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
