namespace DashSpec.Host.Configuration;

public sealed class DashSpecTomlRoot
{
    public DashboardTomlSection Dashboard { get; set; } = new();

    public Dictionary<string, ConnectorTomlSection> Connectors { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public PluginsTomlSection Plugins { get; set; } = new();
}

public sealed class DashboardTomlSection
{
    public string SpecPath { get; set; } = string.Empty;
}

public sealed class ConnectorTomlSection
{
    public string ConnectionString { get; set; } = string.Empty;
}

public sealed class PluginsTomlSection
{
    public string DefaultConnectorId { get; set; } = "sqlserver";

    public List<PluginLoadTomlEntry> Load { get; set; } = [];
}

public sealed class PluginLoadTomlEntry
{
    public string Id { get; set; } = string.Empty;

    public string Assembly { get; set; } = string.Empty;
}
