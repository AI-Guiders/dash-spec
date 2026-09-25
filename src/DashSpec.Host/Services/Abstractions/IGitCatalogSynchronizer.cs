using DashSpec.Host.Configuration;

namespace DashSpec.Host.Services.Abstractions;

public interface IGitCatalogSynchronizer
{
    bool PrepareDeferredSync(DashSpecTomlRoot bootstrap);

    bool TryApply(DashSpecTomlRoot bootstrap, string contentRoot);

    string SyncRepository(CatalogGitTomlSection git, string cacheDir);

    string ResolveCacheDirectory(CatalogGitTomlSection git, string contentRoot);
}
