#nullable enable

using System.Globalization;
using AIGuiders.Platform.CommandPlane;

namespace DashSpec.Host.Commands;

/// <summary>Ambient culture for CCL — from request localization, not hardcoded by Platform.</summary>
public interface IDashboardCultureAmbient : ICultureAmbient;

public sealed class DashboardCultureAmbient : IDashboardCultureAmbient
{
    public CultureInfo Culture { get; }

    public DashboardCultureAmbient()
        : this(CultureInfo.CurrentCulture)
    {
    }

    public DashboardCultureAmbient(CultureInfo culture) => Culture = culture;

    public DashboardCultureAmbient(IHttpContextAccessor? httpContextAccessor)
    {
        var requestCulture = httpContextAccessor?.HttpContext?.Features
            .Get<Microsoft.AspNetCore.Localization.IRequestCultureFeature>()?
            .RequestCulture
            .Culture;
        Culture = requestCulture ?? CultureInfo.CurrentCulture;
    }
}
