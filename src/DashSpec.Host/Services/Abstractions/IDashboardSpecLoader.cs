using DashSpec.Host.Services.Models;

namespace DashSpec.Host.Services.Abstractions;

public interface IDashboardSpecLoader
{
    Task<LoadedDashboard> LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel,
        CancellationToken cancellationToken = default);
}
