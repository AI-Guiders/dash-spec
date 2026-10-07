using DashSpec.Abstractions.Connectors;

namespace DashSpec.Execution.Runtime.Platform;

/// <summary>Resolve data connector for a runtime manifest (host/plugin registry provides impl).</summary>
public interface IReportConnectorResolver
{
    IDataSourceConnector Resolve(string runtimeConfigPath, string? connectorId);
}
