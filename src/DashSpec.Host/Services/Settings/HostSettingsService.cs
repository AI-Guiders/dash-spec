using System.Security.Cryptography;
using System.Text;
using DashSpec.Host.Configuration;
using DashSpec.Host.Data;
using Microsoft.EntityFrameworkCore;

namespace DashSpec.Host.Services.Settings;

public sealed class HostSettingsService(
    DashSpecHostDbContext db,
    DashSpecTomlRoot bootstrap,
    DashSpecAccessOptions accessOptions)
{
    public IReadOnlyDictionary<string, string> GetSection(string section) =>
        db.HostSettings.AsNoTracking()
            .Where(x => x.Section == section)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);

    public bool HasSecret(string section, string key) =>
        db.HostSettings.AsNoTracking()
            .Any(x => x.Section == section && x.Key == key && x.Value.Length > 0);

    public void Upsert(string section, string key, string value, string? updatedBy = "control-center")
    {
        section = section.Trim();
        key = key.Trim();
        var row = db.HostSettings.FirstOrDefault(x => x.Section == section && x.Key == key);
        if (row is null)
        {
            db.HostSettings.Add(new HostSettingEntity
            {
                Section = section,
                Key = key,
                Value = value,
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = updatedBy,
            });
        }
        else
        {
            row.Value = value;
            row.UpdatedAt = DateTimeOffset.UtcNow;
            row.UpdatedBy = updatedBy;
        }

        db.SaveChanges();
        ApplyLive(section, key, value);
    }

    /// <summary>Empty incoming secret keeps existing value.</summary>
    public void UpsertSecret(string section, string key, string? incoming, string? updatedBy = "control-center")
    {
        if (string.IsNullOrWhiteSpace(incoming))
        {
            return;
        }

        Upsert(section, key, incoming.Trim(), updatedBy);
    }

    public string GenerateSyncWebhookSecret()
    {
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        Upsert(HostSettingsOverlay.SectionCatalogGit, "sync_webhook_secret", secret);
        return secret;
    }

    public string ExportTomlFragment()
    {
        var rows = db.HostSettings.AsNoTracking().ToList();
        var sb = new StringBuilder();
        sb.AppendLine("# DashSpec Host settings export (WitDB)");
        WriteSection(sb, rows, HostSettingsOverlay.SectionAccess, "access", redact: true);
        WriteSection(sb, rows, HostSettingsOverlay.SectionCatalogGit, "catalog_git", redact: true);
        return sb.ToString();
    }

    private void ApplyLive(string section, string key, string value)
    {
        if (string.Equals(section, HostSettingsOverlay.SectionAccess, StringComparison.OrdinalIgnoreCase)
            && string.Equals(key, "api_key", StringComparison.OrdinalIgnoreCase))
        {
            bootstrap.Access.ApiKey = value;
            accessOptions.ApiKey = value;
            return;
        }

        if (!string.Equals(section, HostSettingsOverlay.SectionCatalogGit, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var git = bootstrap.CatalogGit;
        switch (key)
        {
            case "enabled" when bool.TryParse(value, out var enabled):
                git.Enabled = enabled;
                break;
            case "url":
                git.Url = value;
                break;
            case "branch":
                git.Branch = value;
                break;
            case "path":
                git.Path = value;
                break;
            case "pull_interval_minutes" when int.TryParse(value, out var minutes) && minutes > 0:
                git.PullIntervalMinutes = minutes;
                break;
            case "cache_directory":
                git.CacheDirectory = value;
                break;
            case "username":
                git.Username = value;
                break;
            case "password":
                git.Password = value;
                break;
            case "sync_webhook_secret":
                git.SyncWebhookSecret = value;
                break;
            case "sync_repo_slug":
                git.SyncRepoSlug = value;
                break;
            case "sync_allow_unsigned" when bool.TryParse(value, out var unsigned):
                git.SyncAllowUnsigned = unsigned;
                break;
        }
    }

    private static void WriteSection(
        StringBuilder sb,
        List<HostSettingEntity> rows,
        string section,
        string tomlSection,
        bool redact)
    {
        var items = rows
            .Where(r => string.Equals(r.Section, section, StringComparison.OrdinalIgnoreCase))
            .OrderBy(r => r.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (items.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.Append('[').Append(tomlSection).AppendLine("]");
        foreach (var item in items)
        {
            var value = redact && IsSecretKey(item.Key) ? "***" : item.Value.Replace("\"", "\\\"", StringComparison.Ordinal);
            if (item.Key is "enabled" or "sync_allow_unsigned"
                || (item.Key == "pull_interval_minutes" && int.TryParse(item.Value, out _)))
            {
                sb.Append(item.Key).Append(" = ").AppendLine(item.Value);
            }
            else
            {
                sb.Append(item.Key).Append(" = \"").Append(value).AppendLine("\"");
            }
        }
    }

    private static bool IsSecretKey(string key) =>
        key is "api_key" or "password" or "sync_webhook_secret";
}
