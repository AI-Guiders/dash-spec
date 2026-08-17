using DashSpec.Core.Parsing;
using DashSpec.Host.Configuration;
using DashSpec.Host.Services.Dev;

namespace DashSpec.Host.Services.Git;

public sealed class GitCatalogSyncBackgroundService(
    DashSpecTomlRoot bootstrap,
    CatalogSourceState catalogState,
    IWebHostEnvironment environment,
    DevSpecReloadNotifier reloadNotifier,
    ILogger<GitCatalogSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!bootstrap.CatalogGit.Enabled || string.IsNullOrWhiteSpace(bootstrap.CatalogGit.Url))
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, bootstrap.CatalogGit.PullIntervalMinutes));
        var cacheDir = GitCatalogSynchronizer.ResolveCacheDirectory(bootstrap.CatalogGit, environment.ContentRootPath);
        var lastCatalogHash = File.Exists(catalogState.Current.FullPath)
            ? await File.ReadAllBytesAsync(catalogState.Current.FullPath, stoppingToken)
            : [];

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                GitCatalogSynchronizer.SyncRepository(bootstrap.CatalogGit, cacheDir, logger);
                var catalogPath = Path.GetFullPath(
                    Path.Combine(cacheDir, bootstrap.CatalogGit.Path.Replace('/', Path.DirectorySeparatorChar)));
                if (!File.Exists(catalogPath) && !catalogPath.EndsWith(".dashcatalog", StringComparison.OrdinalIgnoreCase))
                {
                    catalogPath += ".dashcatalog";
                }

                var bytes = await File.ReadAllBytesAsync(catalogPath, stoppingToken);
                if (bytes.AsSpan().SequenceEqual(lastCatalogHash))
                {
                    continue;
                }

                lastCatalogHash = bytes;
                var document = CatalogParser.ParseFile(catalogPath);
                catalogState.Replace(new CatalogBootstrap(document, catalogPath));
                logger.LogInformation("Git catalog updated, requesting reload: {Path}", catalogPath);
                reloadNotifier.Notify();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Git catalog pull failed; will retry in {Minutes} min", interval.TotalMinutes);
            }
        }
    }
}
