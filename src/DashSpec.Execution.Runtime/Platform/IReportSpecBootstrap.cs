using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Model;
using DashSpec.Core.Platform;

namespace DashSpec.Execution.Runtime.Platform;

/// <summary>Load parsed document, library, filters, and connector from spec text (ADR-0099).</summary>
public interface IReportSpecBootstrap
{
    Task<ReportBootstrapResult> LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel,
        CancellationToken cancellationToken = default,
        ReportLoadOptions? options = null);

    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadFieldOptionsAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        string runtimeConfigPath,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null);
}

