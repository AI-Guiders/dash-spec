using System.Globalization;

namespace DashSpec.Core.Localization;

/// <summary>BCL culture names for DashSpec (host + report). Specific cultures only (<c>ru-RU</c>, <c>en-US</c>, …).</summary>
public static class DashSpecCultures
{
    /// <summary>Cold-start default in bootstrap TOML / dashhost when unset — not a silent runtime fallback.</summary>
    public const string BootstrapDefaultName = "ru-RU";

    /// <summary>Parse a specific culture name (must include region, e.g. <c>en-GB</c>).</summary>
    public static CultureInfo Parse(string cultureName)
    {
        if (string.IsNullOrWhiteSpace(cultureName))
        {
            throw new ArgumentException("Culture name is required.", nameof(cultureName));
        }

        var trimmed = cultureName.Trim();
        if (!trimmed.Contains('-', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Use a specific culture name (e.g. {BootstrapDefaultName}, en-US), not '{trimmed}'.",
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
