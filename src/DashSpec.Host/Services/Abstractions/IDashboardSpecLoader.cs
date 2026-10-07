using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Model;
using DashSpec.Core.Platform;
using DashSpec.Execution.Runtime.Platform;

namespace DashSpec.Host.Services.Abstractions;

/// <summary>Viewer loader returning <see cref="ReportBootstrapResult"/> (Host adapter over platform bootstrap).</summary>
public interface IDashboardSpecLoader : IReportSpecBootstrap
{
    new Task<ReportBootstrapResult> LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel,
        CancellationToken cancellationToken = default,
        ReportLoadOptions? options = null);

    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadFieldOptionsAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null);
}
