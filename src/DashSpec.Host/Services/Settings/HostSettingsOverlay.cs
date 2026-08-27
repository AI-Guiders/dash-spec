using DashSpec.Host.Configuration;
using DashSpec.Host.Data;
using Microsoft.EntityFrameworkCore;
using OutWit.Database.EntityFramework.Extensions;

namespace DashSpec.Host.Services.Settings;

/// <summary>Apply WitDB host_settings onto bootstrap (DASHSPEC-ADR-0042). Env still wins later.</summary>
public static class HostSettingsOverlay
{
    public const string SectionAccess = "access";
    public const string SectionCatalogGit = "catalog_git";

    public static void Apply(DashSpecTomlRoot bootstrap)
    {
        var dbPath = HostSettingsPaths.ResolveDatabasePath(bootstrap);
        try
        {
            HostSettingsPaths.EnsureDatabase(dbPath);
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
        ApplyCatalogGit(bootstrap, rows);
    }

    private static void ApplyAccess(DashSpecTomlRoot bootstrap, List<HostSettingEntity> rows)
    {
        var apiKey = Get(rows, SectionAccess, "api_key");
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            bootstrap.Access.ApiKey = apiKey;
        }
    }

    private static void ApplyCatalogGit(DashSpecTomlRoot bootstrap, List<HostSettingEntity> rows)
    {
        var git = bootstrap.CatalogGit;
        if (TryBool(Get(rows, SectionCatalogGit, "enabled"), out var enabled))
        {
            git.Enabled = enabled;
        }

        SetIfPresent(rows, SectionCatalogGit, "url", v => git.Url = v);
        SetIfPresent(rows, SectionCatalogGit, "branch", v => git.Branch = v);
        SetIfPresent(rows, SectionCatalogGit, "path", v => git.Path = v);
        SetIfPresent(rows, SectionCatalogGit, "cache_directory", v => git.CacheDirectory = v);
        SetIfPresent(rows, SectionCatalogGit, "username", v => git.Username = v);
        SetIfPresent(rows, SectionCatalogGit, "password", v => git.Password = v);
        SetIfPresent(rows, SectionCatalogGit, "sync_webhook_secret", v => git.SyncWebhookSecret = v);
        SetIfPresent(rows, SectionCatalogGit, "sync_repo_slug", v => git.SyncRepoSlug = v);

        if (int.TryParse(Get(rows, SectionCatalogGit, "pull_interval_minutes"), out var minutes) && minutes > 0)
        {
            git.PullIntervalMinutes = minutes;
        }

        if (TryBool(Get(rows, SectionCatalogGit, "sync_allow_unsigned"), out var unsigned))
        {
            git.SyncAllowUnsigned = unsigned;
        }
    }

    private static string? Get(List<HostSettingEntity> rows, string section, string key) =>
        rows.FirstOrDefault(r =>
                string.Equals(r.Section, section, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.Key, key, StringComparison.OrdinalIgnoreCase))
            ?.Value;

    private static void SetIfPresent(
        List<HostSettingEntity> rows,
        string section,
        string key,
        Action<string> assign)
    {
        var value = Get(rows, section, key);
        if (!string.IsNullOrWhiteSpace(value))
        {
            assign(value);
        }
    }

    private static bool TryBool(string? raw, out bool value)
    {
        value = false;
        return !string.IsNullOrWhiteSpace(raw) && bool.TryParse(raw, out value);
    }
}
