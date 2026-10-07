using DashSpec.Abstractions.Connectors;
using DashSpec.Execution.Runtime.Platform;
using DashSpec.Host.Services.Connectors;

namespace DashSpec.Host.Services.Platform;

public sealed class HostReportConnectorResolver(RuntimeConnectorResolver inner) : IReportConnectorResolver
{
    public IDataSourceConnector Resolve(string runtimeConfigPath, string? connectorId) =>
        inner.Resolve(runtimeConfigPath, connectorId);
}
