using DashSpec.Surface.Blazor.Resources;
using Microsoft.Extensions.Localization;

namespace DashSpec.Surface.Blazor.Services.Localization;

/// <summary>
/// UI copy SSOT (ADR-0077): resx-backed localizer over <see cref="HostStrings"/>.
/// Culture comes from <see cref="System.Globalization.CultureInfo.CurrentUICulture"/>,
/// set by RequestLocalization middleware and <c>CultureInfo.DefaultThreadCurrentUICulture</c> (Blazor Server circuits).
/// </summary>
public sealed class DashboardLocalizer(IStringLocalizer<HostStrings> localizer)
{
    /// <summary>Localized string for an English key (the key itself is the fallback).</summary>
    public string T(string key) => localizer[key];

    /// <summary>Localized message with resx format placeholders (<c>{0}</c>, <c>{1}</c>, …).</summary>
    public string T(string key, params object?[] args) =>
        args.Length == 0
            ? localizer[key]
            : localizer[key, Array.ConvertAll(args, static value => value!)];
}
