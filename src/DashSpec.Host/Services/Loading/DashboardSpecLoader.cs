using DashSpec.Abstractions.Connectors;
using DashSpec.Abstractions.Hosting;
using DashSpec.Core.Model;
using DashSpec.Core.Platform;
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

    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadFieldOptionsAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null) =>
        bootstrap.LoadFieldOptionsAsync(
            document,
            connector,
            runtimeContext.StartupRuntimeConfigPath,
            cancellationToken,
            timeout);

    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> IReportSpecBootstrap.LoadFieldOptionsAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        string runtimeConfigPath,
        CancellationToken cancellationToken,
        TimeSpan? timeout) =>
        bootstrap.LoadFieldOptionsAsync(document, connector, runtimeConfigPath, cancellationToken, timeout);
}
