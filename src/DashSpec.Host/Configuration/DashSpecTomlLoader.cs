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

    public static IEnumerable<KeyValuePair<string, string?>> Flatten(DashSpecTomlRoot root)
    {
        if (!string.IsNullOrWhiteSpace(root.Dashboard.SpecPath))
        {
            yield return new KeyValuePair<string, string?>("Dashboard:SpecPath", root.Dashboard.SpecPath);
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
