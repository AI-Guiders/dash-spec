using System.Text;
using System.Text.Json;
using DashSpec.Host.Services.Abstractions;
using Tomlyn;

namespace DashSpec.Host.Configuration;

public sealed class DashSpecTomlLoader : IDashSpecTomlLoader
{
    private static readonly TomlSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public DashSpecTomlRoot LoadFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("DashSpec config not found.", path);
        }

        var text = File.ReadAllText(path, Encoding.UTF8);
        RejectLegacyConnectorTomlKeys(text, path);
        return TomlSerializer.Deserialize<DashSpecTomlRoot>(text, SerializerOptions)
            ?? new DashSpecTomlRoot();
    }

    private static void RejectLegacyConnectorTomlKeys(string text, string path)
    {
        if (text.Contains("[connectors.", StringComparison.OrdinalIgnoreCase)
            || text.Contains("default_connector_id", StringComparison.OrdinalIgnoreCase)
            || text.Contains("is_connector", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Runtime manifest '{path}' uses removed keys ([connectors.*], default_connector_id, is_connector). " +
                "Use [providers.*], default_provider_id, is_provider.");
        }
    }

    public DashSpecTomlRoot Merge(DashSpecTomlRoot root, DashSpecTomlRoot overlay)
    {
        if (!string.IsNullOrWhiteSpace(overlay.Host.Dashhost))
        {
            root.Host.Dashhost = overlay.Host.Dashhost;
        }

        if (!string.IsNullOrWhiteSpace(overlay.Host.DatabasePath))
        {
            root.Host.DatabasePath = overlay.Host.DatabasePath;
        }

        MergeCatalogGit(root.CatalogGit, overlay.CatalogGit);

        if (!string.IsNullOrWhiteSpace(overlay.Access.ApiKey))
        {
            root.Access.ApiKey = overlay.Access.ApiKey;
        }

        foreach (var (providerId, section) in overlay.Providers)
        {
            if (!root.Providers.TryGetValue(providerId, out var existing))
            {
                root.Providers[providerId] = section;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(section.ConnectionString))
            {
                existing.ConnectionString = section.ConnectionString;
            }

            if (section.CommandTimeoutSeconds > 0)
            {
                existing.CommandTimeoutSeconds = section.CommandTimeoutSeconds;
            }

            if (section.MaxRows > 0)
            {
                existing.MaxRows = section.MaxRows;
            }
        }

        if (!string.IsNullOrWhiteSpace(overlay.Plugins.DefaultProviderId))
        {
            root.Plugins.DefaultProviderId = overlay.Plugins.DefaultProviderId;
        }

        if (overlay.Plugins.Load.Count > 0)
        {
            root.Plugins.Load = overlay.Plugins.Load;
        }

        if (overlay.Plugins.Bundles.Count > 0)
        {
            root.Plugins.Bundles = overlay.Plugins.Bundles;
        }

        if (!string.IsNullOrWhiteSpace(overlay.Plugins.ActiveBundle))
        {
            root.Plugins.ActiveBundle = overlay.Plugins.ActiveBundle;
        }

        return root;
    }

    public IEnumerable<KeyValuePair<string, string?>> Flatten(DashSpecTomlRoot root)
    {
        if (!string.IsNullOrWhiteSpace(root.Dashboard.CatalogPath))
        {
            yield return new KeyValuePair<string, string?>("Dashboard:CatalogPath", root.Dashboard.CatalogPath);
        }

        foreach (var (providerId, section) in root.Providers)
        {
            if (!string.IsNullOrWhiteSpace(section.ConnectionString))
            {
                yield return new KeyValuePair<string, string?>(
                    $"Providers:{ToPascalCase(providerId)}:ConnectionString",
                    section.ConnectionString);
            }

            if (section.CommandTimeoutSeconds > 0)
            {
                yield return new KeyValuePair<string, string?>(
                    $"Providers:{ToPascalCase(providerId)}:CommandTimeoutSeconds",
                    section.CommandTimeoutSeconds.ToString());
            }

            if (section.MaxRows > 0)
            {
                yield return new KeyValuePair<string, string?>(
                    $"Providers:{ToPascalCase(providerId)}:MaxRows",
                    section.MaxRows.ToString());
            }
        }

        if (!string.IsNullOrWhiteSpace(root.Plugins.DefaultProviderId))
        {
            yield return new KeyValuePair<string, string?>(
                "DashSpec:DefaultProviderId",
                root.Plugins.DefaultProviderId);
        }

        for (var i = 0; i < root.Plugins.Load.Count; i++)
        {
            var entry = root.Plugins.Load[i];
            yield return new KeyValuePair<string, string?>(
                $"DashSpec:Plugins:{i}:Id",
                entry.Id);
            yield return new KeyValuePair<string, string?>(
                $"DashSpec:Plugins:{i}:Assembly",
                entry.Assembly);
        }
    }

    private static void MergeCatalogGit(CatalogGitTomlSection root, CatalogGitTomlSection overlay)
    {
        if (overlay.Enabled)
        {
            root.Enabled = true;
        }

        if (!string.IsNullOrWhiteSpace(overlay.Url))
        {
            root.Url = overlay.Url;
        }

        if (!string.IsNullOrWhiteSpace(overlay.Branch))
        {
            root.Branch = overlay.Branch;
        }

        if (!string.IsNullOrWhiteSpace(overlay.Path))
        {
            root.Path = overlay.Path;
        }

        if (overlay.PullIntervalMinutes > 0)
        {
            root.PullIntervalMinutes = overlay.PullIntervalMinutes;
        }

        if (!string.IsNullOrWhiteSpace(overlay.CacheDirectory))
        {
            root.CacheDirectory = overlay.CacheDirectory;
        }

        if (!string.IsNullOrWhiteSpace(overlay.Username))
        {
            root.Username = overlay.Username;
        }

        if (!string.IsNullOrWhiteSpace(overlay.Password))
        {
            root.Password = overlay.Password;
        }

        if (!string.IsNullOrWhiteSpace(overlay.SyncWebhookSecret))
        {
            root.SyncWebhookSecret = overlay.SyncWebhookSecret;
        }

        if (overlay.SyncAllowUnsigned)
        {
            root.SyncAllowUnsigned = true;
        }

        if (!string.IsNullOrWhiteSpace(overlay.SyncRepoSlug))
        {
            root.SyncRepoSlug = overlay.SyncRepoSlug;
        }
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.Equals("sqlserver", StringComparison.OrdinalIgnoreCase))
        {
            return "SqlServer";
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }
}
