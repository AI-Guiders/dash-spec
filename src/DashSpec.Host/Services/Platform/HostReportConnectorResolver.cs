using DashSpec.Abstractions.Connectors;
using DashSpec.Execution.Runtime.Platform;
using DashSpec.Host.Services.Connectors;
using DashSpec.Host.Services.Telemetry;

namespace DashSpec.Host.Services.Platform;

public sealed class HostReportConnectorResolver(
    RuntimeConnectorResolver inner,
    HostTelemetryStore telemetryStore) : IReportConnectorResolver
{
    public IDataSourceConnector Resolve(string runtimeConfigPath, string? connectorId)
    {
        var connector = inner.Resolve(runtimeConfigPath, connectorId);
        if (string.Equals(connector.Id, "witdb", StringComparison.OrdinalIgnoreCase)
            || connector is TelemetryRecordingConnector)
        {
            return connector;
        }

        return new TelemetryRecordingConnector(connector, telemetryStore);
    }
}
