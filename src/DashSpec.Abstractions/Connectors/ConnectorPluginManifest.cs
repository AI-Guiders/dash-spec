namespace DashSpec.Abstractions.Connectors;

public sealed class ConnectorPluginManifest
{
    public string DefaultConnectorId { get; set; } = "sqlserver";

    public List<ConnectorPluginEntry> Plugins { get; set; } = [];
}

public sealed class ConnectorPluginEntry
{
    public string Id { get; set; } = string.Empty;

    public string Assembly { get; set; } = string.Empty;
}
