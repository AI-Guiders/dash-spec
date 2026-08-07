using System.Collections.Concurrent;
using DashSpec.Abstractions.Connectors;
using DashSpec.Connector.SqlServer;
using DashSpec.Host.Configuration;
using DashSpec.Host.Plugins;
using Microsoft.Extensions.Options;

namespace DashSpec.Host.Services.Connectors;

/// <summary>
/// Resolves <see cref="IDataSourceConnector"/> from the catalog entry's @runtime TOML.
/// Startup DI connector covers the default entry; other entries may point at another DB.
/// </summary>
public sealed class RuntimeConnectorResolver(
    ConnectorRegistry connectorRegistry,
    ConnectorPluginManifest pluginManifest,
    DashSpecHostContext hostContext)
{
    private readonly ConcurrentDictionary<string, IDataSourceConnector> _byKey =
        new(StringComparer.OrdinalIgnoreCase);

    public IDataSourceConnector Resolve(string runtimeConfigPath, string? connectorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeConfigPath);

        var id = string.IsNullOrWhiteSpace(connectorId)
            ? pluginManifest.DefaultConnectorId
            : connectorId;

        if (string.Equals(
                runtimeConfigPath,
                hostContext.StartupRuntimeConfigPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return connectorRegistry.Resolve(id, pluginManifest.DefaultConnectorId);
        }

        var key = $"{runtimeConfigPath}::{id}";
        return _byKey.GetOrAdd(key, static (k, state) => state.Create(k), this);
    }

    private IDataSourceConnector Create(string key)
    {
        var sep = key.LastIndexOf("::", StringComparison.Ordinal);
        var runtimeConfigPath = key[..sep];
        var connectorId = key[(sep + 2)..];

        var runtime = DashSpecTomlLoader.LoadFile(runtimeConfigPath);

        if (!TryGetConnectorSection(runtime, connectorId, out var section) ||
            string.IsNullOrWhiteSpace(section.ConnectionString))
        {
            var available = string.Join(
                ", ",
                runtime.Connectors.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            throw new InvalidOperationException(
                $"Runtime '{Path.GetFileName(runtimeConfigPath)}' has no connection_string for connector '{connectorId}'. " +
                $"Available: {(string.IsNullOrEmpty(available) ? "(none)" : available)}.");
        }

        if (!connectorId.Equals("sqlserver", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Per-entry runtime binding supports connector 'sqlserver' only (got '{connectorId}').");
        }

        return new SqlServerConnector(Options.Create(new SqlServerConnectorOptions
        {
            ConnectionString = section.ConnectionString,
            CommandTimeoutSeconds = section.CommandTimeoutSeconds,
            MaxRows = section.MaxRows,
        }));
    }

    private static bool TryGetConnectorSection(
        DashSpecTomlRoot runtime,
        string connectorId,
        out ConnectorTomlSection section)
    {
        if (runtime.Connectors.TryGetValue(connectorId, out section!))
        {
            return true;
        }

        section = null!;
        return false;
    }
}
