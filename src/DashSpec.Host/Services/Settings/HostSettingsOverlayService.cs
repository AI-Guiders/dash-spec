using DashSpec.Core.Resolution;
using DashSpec.Host.Configuration;
using DashSpec.Host.Data;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Presentation;
using Microsoft.EntityFrameworkCore;
using OutWit.Database.EntityFramework.Extensions;

namespace DashSpec.Host.Services.Settings;

/// <summary>Apply WitDB host_settings onto bootstrap (DASHSPEC-ADR-0042).</summary>
public sealed class HostSettingsOverlayService(IHostDatabaseInitializer hostDatabase) : IHostSettingsOverlay
{
    public void Apply(DashSpecTomlRoot bootstrap)
    {
        var dbPath = hostDatabase.ResolveDatabasePath(bootstrap);
        try
        {
            hostDatabase.EnsureDatabase(dbPath);
        }
        catch
        {
            // Missing provider / ACL — keep toml-only bootstrap.
            return;
        }

        var options = new DbContextOptionsBuilder<DashSpecHostDbContext>()
            .UseWitDb($"Data Source={dbPath}")
            .Options;
        using var db = new DashSpecHostDbContext(options);
        var rows = db.HostSettings.AsNoTracking().ToList();
        if (rows.Count == 0)
        {
            return;
        }

        ApplyAccess(bootstrap, rows);
        ApplyPresentation(bootstrap, rows);
    }

    private static void ApplyAccess(DashSpecTomlRoot bootstrap, List<HostSettingEntity> rows)
    {
        var apiKey = Get(rows, HostSettingsSections.SectionAccess, "api_key");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            bootstrap.Access.ApiKey = apiKey;
        }
    }

    private static void ApplyPresentation(DashSpecTomlRoot bootstrap, List<HostSettingEntity> rows)
    {
        var tz = Get(rows, HostSettingsSections.SectionPresentation, HostSettingsSections.KeyDisplayTimeZone);
        bootstrap.Presentation.DisplayTimeZone = HostOpsResolution.ResolveDisplayTimeZone(
            bootstrap.Presentation.DisplayTimeZone,
            bootstrap.Presentation.DisplayTimeZone,
            string.IsNullOrWhiteSpace(tz) ? null : tz.Trim());

        var scheme = Get(rows, HostSettingsSections.SectionPresentation, HostSettingsSections.KeyColorScheme);
        bootstrap.Presentation.ColorScheme = HostOpsResolution.ResolveColorScheme(
            bootstrap.Presentation.ColorScheme,
            bootstrap.Presentation.ColorScheme,
            string.IsNullOrWhiteSpace(scheme) ? null : scheme.Trim().ToLowerInvariant());

        var language = Get(rows, HostSettingsSections.SectionPresentation, HostSettingsSections.KeyLanguage);
        bootstrap.Presentation.Language = HostOpsResolution.ResolveLanguage(
            bootstrap.Presentation.Language,
            bootstrap.Presentation.Language,
            string.IsNullOrWhiteSpace(language) ? null : language.Trim().ToLowerInvariant());

        var largeLayout = Get(rows, HostSettingsSections.SectionPresentation, HostSettingsSections.KeyLargeFieldFilterLayout);
        if (!string.IsNullOrWhiteSpace(largeLayout))
        {
            bootstrap.Presentation.LargeFieldFilterLayout = FilterLargeListOptions.Normalize(largeLayout);
        }
    }

    private static string? Get(List<HostSettingEntity> rows, string section, string key) =>
        rows.FirstOrDefault(r =>
                string.Equals(r.Section, section, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase))
            ?.Value;
}
