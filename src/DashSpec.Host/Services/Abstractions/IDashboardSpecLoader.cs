using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Model;
using DashSpec.Core.Platform;
using DashSpec.Execution.Runtime.Platform;
using DashSpec.Host.Services.Models;

namespace DashSpec.Host.Services.Abstractions;

public interface IDashboardSpecLoader : IReportSpecBootstrap
{
    new Task<LoadedDashboard> LoadFromTextAsync(
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
