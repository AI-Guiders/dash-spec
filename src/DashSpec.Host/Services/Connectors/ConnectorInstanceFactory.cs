using DashSpec.Abstractions.Connectors;
using DashSpec.Connector.Postgres;
using DashSpec.Connector.SqlServer;
using DashSpec.Host.Configuration;
using Microsoft.Extensions.Options;

namespace DashSpec.Host.Services.Connectors;

/// <summary>Constructs connector instances for per-entry runtime TOML bindings.</summary>
internal static class ConnectorInstanceFactory
{
    public static IDataSourceConnector Create(string providerId, ProviderTomlSection section) =>
        providerId.ToLowerInvariant() switch
        {
            "sqlserver" => new SqlServerConnector(Options.Create(new SqlServerConnectorOptions
            {
                ConnectionString = section.ConnectionString,
                CommandTimeoutSeconds = section.CommandTimeoutSeconds,
                MaxRows = section.MaxRows,
            })),
            "postgres" or "postgresql" => new PostgresConnector(Options.Create(new PostgresConnectorOptions
            {
                ConnectionString = section.ConnectionString,
                CommandTimeoutSeconds = section.CommandTimeoutSeconds,
                MaxRows = section.MaxRows,
            })),
            _ => throw new InvalidOperationException(
                $"Per-entry runtime binding supports provider ids: sqlserver, postgres (got '{providerId}')."),
        };
}
