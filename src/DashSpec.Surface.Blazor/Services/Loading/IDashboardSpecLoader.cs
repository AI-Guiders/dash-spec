using DashSpec.Abstractions.Connectors;
using DashSpec.Execution.Runtime;
using DashSpec.Execution.Runtime.Platform;

namespace DashSpec.Surface.Blazor.Services.Loading;

public interface IDashboardSpecLoader : IReportSpecBootstrap
{
    new Task<ReportBootstrapResult> LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel,
        CancellationToken cancellationToken = default,
        ReportLoadOptions? options = null);

    Task<FieldOptionsCatalog> LoadFieldOptionsAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null,
        string? runtimeConfigPath = null);
}
