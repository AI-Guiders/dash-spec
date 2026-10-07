using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Model;
using DashSpec.Core.Platform;
using DashSpec.Execution.Runtime.Platform;
using DashSpec.Host.Configuration;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Models;

namespace DashSpec.Host.Services.Loading;

public sealed class DashboardSpecLoader(
    IReportSpecBootstrap bootstrap,
    DashSpecHostContext hostContext) : IDashboardSpecLoader
{
    public async Task<LoadedDashboard> LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel,
        CancellationToken cancellationToken = default,
        ReportLoadOptions? options = null)
    {
        var loaded = await bootstrap.LoadFromTextAsync(
            text,
            specFullPath,
            sourceLabel,
            cancellationToken,
            options).ConfigureAwait(false);
        return ToLoadedDashboard(loaded);
    }

    async Task<ReportBootstrapResult> IReportSpecBootstrap.LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel,
        CancellationToken cancellationToken,
        ReportLoadOptions? options) =>
        await bootstrap.LoadFromTextAsync(text, specFullPath, sourceLabel, cancellationToken, options)
            .ConfigureAwait(false);

    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadFieldOptionsAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null) =>
        bootstrap.LoadFieldOptionsAsync(
            document,
            connector,
            hostContext.StartupRuntimeConfigPath,
            cancellationToken,
            timeout);

    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> IReportSpecBootstrap.LoadFieldOptionsAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        string runtimeConfigPath,
        CancellationToken cancellationToken,
        TimeSpan? timeout) =>
        bootstrap.LoadFieldOptionsAsync(document, connector, runtimeConfigPath, cancellationToken, timeout);

    private static LoadedDashboard ToLoadedDashboard(ReportBootstrapResult loaded) =>
        new(
            loaded.Document,
            loaded.Library,
            loaded.Connector,
            loaded.FilterIndex,
            loaded.Filters,
            loaded.FieldOptions,
            loaded.SourceLabel,
            loaded.SpecDirectory,
            loaded.RuntimeConfigPath);
}
