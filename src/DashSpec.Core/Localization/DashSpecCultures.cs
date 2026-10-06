using System.Globalization;

namespace DashSpec.Core.Localization;

/// <summary>BCL culture names for DashSpec (host + report). Not tied to a single product locale.</summary>
public static class DashSpecCultures
{
    public const string FallbackName = "ru-RU";

    /// <summary>Resolve host <c>language</c> or report <c>culture</c> (e.g. <c>en</c>, <c>en-GB</c>, <c>ru-RU</c>).</summary>
    public static CultureInfo Resolve(string? cultureOrLanguage)
    {
        if (string.IsNullOrWhiteSpace(cultureOrLanguage))
        {
            return Get(FallbackName);
        }

        var token = cultureOrLanguage.Trim();
        if (token.Contains('-', StringComparison.Ordinal))
        {
            return Get(token);
        }

        return token.ToLowerInvariant() switch
        {
            "en" => Get("en-US"),
            "ru" => Get("ru-RU"),
            _ => Get(token),
        };
    }

    public static CultureInfo Get(string name) => CultureInfo.GetCultureInfo(name);

    public static DateTime StartOfCalendarWeek(DateTime date, CultureInfo culture)
    {
        var firstDay = culture.DateTimeFormat.FirstDayOfWeek;
        var day = date.Date;
        var offset = (7 + (day.DayOfWeek - firstDay)) % 7;
        return day.AddDays(-offset);
    }
}
