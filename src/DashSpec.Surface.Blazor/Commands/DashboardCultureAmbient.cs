#nullable enable

using System.Globalization;

namespace DashSpec.Surface.Blazor.Commands;

/// <summary>Ambient culture and display time zone for the Blazor viewer surface.</summary>
public interface IDashboardCultureAmbient
{
    CultureInfo Culture { get; }

    /// <summary>Display time zone for report/table output (remark №25: stored UTC → display TZ). Default Europe/Moscow.</summary>
    TimeZoneInfo DisplayTimeZone { get; }
}

public sealed class DashboardCultureAmbient : IDashboardCultureAmbient
{
    public const string DefaultTimeZoneId = "Europe/Moscow";

    public CultureInfo Culture { get; }

    public TimeZoneInfo DisplayTimeZone { get; }

    public DashboardCultureAmbient()
        : this(CultureInfo.CurrentCulture)
    {
    }

    public DashboardCultureAmbient(CultureInfo culture)
        : this(culture, ResolveTimeZone(null))
    {
    }

    public DashboardCultureAmbient(CultureInfo culture, TimeZoneInfo? displayTimeZone)
    {
        Culture = culture;
        DisplayTimeZone = displayTimeZone ?? ResolveTimeZone(null);
    }

    public DashboardCultureAmbient(IHttpContextAccessor? httpContextAccessor)
    {
        var requestCulture = httpContextAccessor?.HttpContext?.Features
            .Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>()?
            .RequestCulture
            .Culture;
        Culture = requestCulture ?? CultureInfo.CurrentCulture;
        DisplayTimeZone = ResolveTimeZone(null);
    }

    public DashboardCultureAmbient(IHttpContextAccessor? httpContextAccessor, string? timeZoneId)
    {
        var requestCulture = httpContextAccessor?.HttpContext?.Features
            .Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>()?
            .RequestCulture
            .Culture;
        Culture = requestCulture ?? CultureInfo.CurrentCulture;
        DisplayTimeZone = ResolveTimeZone(timeZoneId);
    }

    public static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        var id = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
