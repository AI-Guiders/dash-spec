namespace DashSpec.Abstractions.Connectors;

public sealed class ConnectorRegistry(IEnumerable<IDataSourceConnector> connectors)
{
    private readonly IReadOnlyDictionary<string, IDataSourceConnector> _byId =
        connectors.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

    public IDataSourceConnector Resolve(string? providerId, string defaultProviderId)
    {
        var id = string.IsNullOrWhiteSpace(providerId) ? defaultProviderId : providerId;
        if (_byId.TryGetValue(id, out var connector))
        {
            return connector;
        }

        throw new InvalidOperationException(
            $"Data provider '{id}' is not loaded. Available: {string.Join(", ", _byId.Keys.OrderBy(x => x))}.");
    }

    public IReadOnlyCollection<string> LoadedProviderIds => _byId.Keys.ToList();
}
