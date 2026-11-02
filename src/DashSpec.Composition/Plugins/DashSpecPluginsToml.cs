using System.Text.Json;
using DashSpec.Abstractions.Plugins;
using Tomlyn;

namespace DashSpec.Composition.Plugins;

/// <summary>
/// Reads the <c>[plugins]</c> section of a <c>dash-spec.toml</c> into the activation manifest
/// (ADR-0117 step 3): the vocabulary source for roots that do not carry the full Host TOML
/// root (dsc CLI). Host keeps its typed root for the rest of the sections; both produce the
/// same <see cref="DashSpecPluginManifest"/>.
/// </summary>
public static class DashSpecPluginsToml
{
    private static readonly TomlSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static DashSpecPluginManifest Read(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("DashSpec plugin policy TOML not found.", path);
        }

        return ReadString(File.ReadAllText(path));
    }

    public static DashSpecPluginManifest ReadString(string tomlText)
    {
        var root = TomlSerializer.Deserialize<PluginsTomlRoot>(tomlText, SerializerOptions) ?? new();
        var section = root.Plugins ?? new();

        return new DashSpecPluginManifest
        {
            ActiveBundle = string.IsNullOrWhiteSpace(section.ActiveBundle) ? "standard" : section.ActiveBundle,
            DefaultProviderId = string.IsNullOrWhiteSpace(section.DefaultProviderId)
                ? "sqlserver"
                : section.DefaultProviderId,
            Bundles = section.Bundles.Select(x => new DashSpecBundleDefinition
                {
                    Name = x.Name,
                    Plugins = x.Plugins,
                })
                .ToList(),
            Plugins = section.Load.Select(x => new DashSpecPluginLoadEntry
                {
                    Id = x.Id,
                    Assembly = x.Assembly ?? string.Empty,
                    Tier = ParseTier(x.Tier),
                    IsProvider = x.IsProvider,
                })
                .ToList(),
        };
    }

    private static PluginTier ParseTier(string? raw) =>
        raw?.ToLowerInvariant() switch
        {
            "core" => PluginTier.Core,
            "product" => PluginTier.Product,
            _ => PluginTier.Extended,
        };

    private sealed class PluginsTomlRoot
    {
        public PluginsTomlSection? Plugins { get; set; }
    }

    private sealed class PluginsTomlSection
    {
        public string? ActiveBundle { get; set; }

        public string? DefaultProviderId { get; set; }

        public List<PluginsTomlBundle> Bundles { get; set; } = [];

        public List<PluginsTomlLoad> Load { get; set; } = [];
    }

    private sealed class PluginsTomlBundle
    {
        public string Name { get; set; } = string.Empty;

        public List<string> Plugins { get; set; } = [];
    }

    private sealed class PluginsTomlLoad
    {
        public string Id { get; set; } = string.Empty;

        public string? Assembly { get; set; }

        public string? Tier { get; set; }

        public bool IsProvider { get; set; }
    }
}
