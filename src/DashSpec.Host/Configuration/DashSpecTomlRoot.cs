namespace DashSpec.Host.Configuration;

public sealed class DashSpecTomlRoot
{
    public DashboardTomlSection Dashboard { get; set; } = new();

    public AccessTomlSection Access { get; set; } = new();

    public Dictionary<string, ConnectorTomlSection> Connectors { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public PluginsTomlSection Plugins { get; set; } = new();
}

public sealed class AccessTomlSection
{
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class DashboardTomlSection
{
    public string CatalogPath { get; set; } = string.Empty;
}

public sealed class ConnectorTomlSection
{
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>SqlCommand.CommandTimeout (seconds). 0 = connector default (120).</summary>
    public int CommandTimeoutSeconds { get; set; }

    /// <summary>Abort when result row count exceeds limit (0 = connector default 250000).</summary>
    public int MaxRows { get; set; }
}

public sealed class PluginsTomlSection
{
    public string DefaultConnectorId { get; set; } = "sqlserver";

    public string ActiveBundle { get; set; } = "standard";

    public List<PluginBundleTomlEntry> Bundles { get; set; } = [];

    public List<PluginLoadTomlEntry> Load { get; set; } = [];
}

public sealed class PluginBundleTomlEntry
{
    public string Name { get; set; } = string.Empty;

    public List<string> Plugins { get; set; } = [];
}

public sealed class PluginLoadTomlEntry
{
    public string Id { get; set; } = string.Empty;

    public string Assembly { get; set; } = string.Empty;

    public string Tier { get; set; } = "extended";

    public bool IsConnector { get; set; }
}
