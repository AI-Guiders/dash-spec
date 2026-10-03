using System.Collections.Concurrent;
using DashSpec.Abstractions.Connectors;
using DashSpec.Host.Configuration;
using DashSpec.Host.Plugins;
using DashSpec.Host.Services.Abstractions;

namespace DashSpec.Host.Services.Connectors;

/// <summary>
/// Resolves <see cref="IDataSourceConnector"/> from the catalog entry's @runtime TOML.
/// Startup DI connector covers the default entry; other entries may point at another DB.
/// </summary>
public sealed class RuntimeConnectorResolver(
    ConnectorRegistry connectorRegistry,
    ConnectorPluginManifest pluginManifest,
    DashSpecHostContext hostContext,
    IDashSpecTomlLoader tomlLoader)
{
    private readonly ConcurrentDictionary<string, IDataSourceConnector> _byKey =
        new(StringComparer.OrdinalIgnoreCase);

    public IDataSourceConnector Resolve(string runtimeConfigPath, string? providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeConfigPath);

        var id = string.IsNullOrWhiteSpace(providerId)
            ? pluginManifest.DefaultProviderId
            : providerId;

        if (string.Equals(
                runtimeConfigPath,
                hostContext.StartupRuntimeConfigPath,
                StringComparison.OrdinalIgnoreCase))
        {
            return connectorRegistry.Resolve(id, pluginManifest.DefaultProviderId);
        }

        var key = $"{runtimeConfigPath}::{id}";
        return _byKey.GetOrAdd(key, static (k, state) => state.Create(k), this);
    }

    private IDataSourceConnector Create(string key)
    {
        var sep = key.LastIndexOf("::", StringComparison.Ordinal);
        var runtimeConfigPath = key[..sep];
        var providerId = key[(sep + 2)..];

        var runtime = tomlLoader.LoadFile(runtimeConfigPath);

        if (!TryGetProviderSection(runtime, providerId, out var section) ||
            string.IsNullOrWhiteSpace(section.ConnectionString))
        {
            var available = string.Join(
                ", ",
                runtime.Providers.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            throw new InvalidOperationException(
                $"Runtime '{Path.GetFileName(runtimeConfigPath)}' has no connection_string for provider '{providerId}'. " +
                $"Available: {(string.IsNullOrEmpty(available) ? "(none)" : available)}.");
        }

        return ConnectorInstanceFactory.Create(providerId, section);
    }

    private static bool TryGetProviderSection(
        DashSpecTomlRoot runtime,
        string providerId,
        out ProviderTomlSection section)
    {
        if (runtime.Providers.TryGetValue(providerId, out section!))
        {
            return true;
        }

        section = null!;
        return false;
    }
}
