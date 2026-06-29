namespace DashSpec.Abstractions.Connectors;

public sealed class ConnectorRegistry(IEnumerable<IDataSourceConnector> connectors)
{
    private readonly IReadOnlyDictionary<string, IDataSourceConnector> _byId =
        connectors.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

    public IDataSourceConnector Resolve(string? connectorId, string defaultConnectorId)
    {
        var id = string.IsNullOrWhiteSpace(connectorId) ? defaultConnectorId : connectorId;
        if (_byId.TryGetValue(id, out var connector))
        {
            return connector;
        }

        throw new InvalidOperationException(
            $"Connector '{id}' is not loaded. Available: {string.Join(", ", _byId.Keys.OrderBy(x => x))}.");
    }

    public IReadOnlyCollection<string> LoadedConnectorIds => _byId.Keys.ToList();
}
