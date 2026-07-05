using System.Text;
using System.Text.Json;
using Tomlyn;

namespace DashSpec.Host.Configuration;

public static class DashSpecTomlLoader
{
    private static readonly TomlSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static DashSpecTomlRoot LoadFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("DashSpec config not found.", path);
        }

        var text = File.ReadAllText(path, Encoding.UTF8);
        return TomlSerializer.Deserialize<DashSpecTomlRoot>(text, SerializerOptions)
            ?? new DashSpecTomlRoot();
    }

    public static DashSpecTomlRoot Merge(DashSpecTomlRoot root, DashSpecTomlRoot overlay)
    {
        if (!string.IsNullOrWhiteSpace(overlay.Dashboard.CatalogPath))
        {
            root.Dashboard.CatalogPath = overlay.Dashboard.CatalogPath;
        }

        if (!string.IsNullOrWhiteSpace(overlay.Access.ApiKey))
        {
            root.Access.ApiKey = overlay.Access.ApiKey;
        }

        foreach (var (connectorId, section) in overlay.Connectors)
        {
            if (!root.Connectors.TryGetValue(connectorId, out var existing))
            {
                root.Connectors[connectorId] = section;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(section.ConnectionString))
            {
                existing.ConnectionString = section.ConnectionString;
            }
        }

        if (!string.IsNullOrWhiteSpace(overlay.Plugins.DefaultConnectorId))
        {
            root.Plugins.DefaultConnectorId = overlay.Plugins.DefaultConnectorId;
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

    public static IEnumerable<KeyValuePair<string, string?>> Flatten(DashSpecTomlRoot root)
    {
        if (!string.IsNullOrWhiteSpace(root.Dashboard.CatalogPath))
        {
            yield return new KeyValuePair<string, string?>("Dashboard:CatalogPath", root.Dashboard.CatalogPath);
        }

        foreach (var (connectorId, section) in root.Connectors)
        {
            if (!string.IsNullOrWhiteSpace(section.ConnectionString))
            {
                yield return new KeyValuePair<string, string?>(
                    $"Connectors:{ToPascalCase(connectorId)}:ConnectionString",
                    section.ConnectionString);
            }
        }

        if (!string.IsNullOrWhiteSpace(root.Plugins.DefaultConnectorId))
        {
            yield return new KeyValuePair<string, string?>(
                "DashSpec:DefaultConnectorId",
                root.Plugins.DefaultConnectorId);
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
