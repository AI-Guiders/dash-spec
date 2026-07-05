using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Model;
using DashSpec.Host.Services.Loading;
using DashSpec.Host.Services.Models;

namespace DashSpec.Host.Services.Abstractions;

public interface IDashboardSpecLoader
{
    Task<LoadedDashboard> LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel,
        CancellationToken cancellationToken = default,
        SpecLoadOptions? options = null);

    Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadFieldOptionsAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null);
}
