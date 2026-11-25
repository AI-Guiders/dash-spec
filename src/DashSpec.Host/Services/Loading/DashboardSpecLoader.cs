using DashSpec.Abstractions.Connectors;
using DashSpec.Abstractions.Hosting;
using DashSpec.Execution.Runtime;
using DashSpec.Execution.Runtime.Platform;
using DashSpec.Host.Services.Abstractions;

namespace DashSpec.Host.Services.Loading;

public sealed class DashboardSpecLoader(
    IReportSpecBootstrap bootstrap,
    IViewerRuntimeContext runtimeContext) : IDashboardSpecLoader
{
    public Task<ReportBootstrapResult> LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel,
        CancellationToken cancellationToken = default,
        ReportLoadOptions? options = null) =>
        bootstrap.LoadFromTextAsync(text, specFullPath, sourceLabel, cancellationToken, options);

    Task<ReportBootstrapResult> IReportSpecBootstrap.LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel,
        CancellationToken cancellationToken,
        ReportLoadOptions? options) =>
        bootstrap.LoadFromTextAsync(text, specFullPath, sourceLabel, cancellationToken, options);

    public Task<FieldOptionsCatalog> LoadFieldOptionsAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null,
        string? runtimeConfigPath = null) =>
        bootstrap.LoadFieldOptionsAsync(
            document,
            connector,
            string.IsNullOrWhiteSpace(runtimeConfigPath) ? runtimeContext.StartupRuntimeConfigPath : runtimeConfigPath,
            cancellationToken,
            timeout);

    Task<FieldOptionsCatalog> IReportSpecBootstrap.LoadFieldOptionsAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        string runtimeConfigPath,
        CancellationToken cancellationToken,
        TimeSpan? timeout,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? currentSelections) =>
        bootstrap.LoadFieldOptionsAsync(document, connector, runtimeConfigPath, cancellationToken, timeout, currentSelections);
}
