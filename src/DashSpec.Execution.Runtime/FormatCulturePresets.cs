using System.Globalization;
using System.Text.RegularExpressions;
using DashSpec.Core.Localization;
using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

/// <summary>Maps named format presets to BCL patterns for a report culture (ru-RU, en-US, en-GB, …).</summary>
public static partial class FormatCulturePresets
{
    public static CultureInfo ResolveCulture(ReportFormatDefaults? defaults, CultureInfo? uiCulture)
    {
        if (!string.IsNullOrWhiteSpace(defaults?.Culture))
        {
            return DashSpecCultures.Resolve(defaults.Culture);
        }

        return uiCulture ?? DashSpecCultures.Get(DashSpecCultures.FallbackName);
    }

    public static string ResolvePresetPattern(string preset, CultureInfo culture)
    {
        if (!IsNamedPreset(preset))
        {
            return preset;
        }

        var dtf = culture.DateTimeFormat;
        return preset.ToLowerInvariant() switch
        {
            "date.short" => StripYearFromDatePattern(dtf.ShortDatePattern),
            "date.full" => dtf.ShortDatePattern,
            "date.iso" => "yyyy-MM-dd",
            "time.short" => dtf.ShortTimePattern,
            "datetime.short" => $"{StripYearFromDatePattern(dtf.ShortDatePattern)} {dtf.ShortTimePattern}",
            "datetime.iso" => "yyyy-MM-dd HH:mm",
            _ => preset,
        };
    }

    public static bool IsNamedPreset(string format) =>
        format.Equals("date.short", StringComparison.OrdinalIgnoreCase)
        || format.Equals("date.full", StringComparison.OrdinalIgnoreCase)
        || format.Equals("date.iso", StringComparison.OrdinalIgnoreCase)
        || format.Equals("time.short", StringComparison.OrdinalIgnoreCase)
        || format.Equals("datetime.short", StringComparison.OrdinalIgnoreCase)
        || format.Equals("datetime.iso", StringComparison.OrdinalIgnoreCase);

    internal static string StripYearFromDatePattern(string pattern)
    {
        var stripped = YearStripRegex().Replace(pattern, string.Empty);
        return stripped.Trim(' ', '.', '-', '/', ',');
    }

    [GeneratedRegex(@"[\s\.\-/]*'?y+'?[\s\.\-/]*", RegexOptions.IgnoreCase)]
    private static partial Regex YearStripRegex();
}
