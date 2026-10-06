using System.Globalization;

namespace DashSpec.Core.Localization;

/// <summary>BCL culture names for DashSpec (host + report). Specific cultures only (<c>ru-RU</c>, <c>en-US</c>, …).</summary>
public static class DashSpecCultures
{
    /// <summary>Cold-start default in bootstrap TOML / dashhost when unset — not a silent runtime fallback.</summary>
    public const string BootstrapDefaultName = "ru-RU";

    /// <summary>Upgrade stored shorthand (<c>ru</c>, <c>en</c>) to specific culture names. WitDB / legacy dashhost.</summary>
    public static string NormalizeStoredLanguage(string? cultureOrLanguage)
    {
        if (string.IsNullOrWhiteSpace(cultureOrLanguage))
        {
            return string.Empty;
        }

        var token = cultureOrLanguage.Trim();
        if (token.Contains('-', StringComparison.Ordinal))
        {
            return token;
        }

        return token.ToLowerInvariant() switch
        {
            "en" => "en-US",
            "ru" => "ru-RU",
            _ => token,
        };
    }

    /// <summary>Parse a specific culture name (must include region, e.g. <c>en-GB</c>).</summary>
    public static CultureInfo Parse(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            throw new ArgumentException("Culture name is required.", nameof(cultureName));
        }

        var trimmed = NormalizeStoredLanguage(cultureName);
        if (string.IsNullOrWhiteSpace(trimmed) || !trimmed.Contains('-', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Use a specific culture name (e.g. {BootstrapDefaultName}, en-US), not '{cultureName}'.",
                nameof(cultureName));
        }

        return CultureInfo.GetCultureInfo(trimmed);
    }

    public static DateTime StartOfCalendarWeek(DateTime date, CultureInfo culture)
    {
        var firstDay = culture.DateTimeFormat.FirstDayOfWeek;
        var day = date.Date;
        var offset = (7 + (day.DayOfWeek - firstDay)) % 7;
        return day.AddDays(-offset);
    }
}
