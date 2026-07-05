namespace DashSpec.Abstractions.Plugins;

public sealed class DashSpecPluginManifest
{
    public string ActiveBundle { get; set; } = "standard";

    public List<DashSpecBundleDefinition> Bundles { get; set; } = [];

    public List<DashSpecPluginLoadEntry> Plugins { get; set; } = [];

    public string DefaultConnectorId { get; set; } = "sqlserver";
}

public sealed class DashSpecBundleDefinition
{
    public string Name { get; set; } = string.Empty;

    public List<string> Plugins { get; set; } = [];
}

public sealed class DashSpecPluginLoadEntry
{
    public string Id { get; set; } = string.Empty;

    public string Assembly { get; set; } = string.Empty;

    public PluginTier Tier { get; set; } = PluginTier.Extended;

    public bool IsConnector { get; set; }
}
