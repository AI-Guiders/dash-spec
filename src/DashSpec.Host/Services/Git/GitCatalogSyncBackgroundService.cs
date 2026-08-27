using DashSpec.Host.Configuration;

namespace DashSpec.Host.Services.Git;

public sealed class GitCatalogSyncBackgroundService(
    DashSpecTomlRoot bootstrap,
    GitCatalogSyncService syncService,
    ILogger<GitCatalogSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!syncService.IsEnabled)
        {
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, bootstrap.CatalogGit.PullIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
                var result = await syncService.SyncAsync(stoppingToken).ConfigureAwait(false);
                if (result.Status == "busy")
                {
                    logger.LogDebug("Git catalog poll skipped — sync already in progress");
                }
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
