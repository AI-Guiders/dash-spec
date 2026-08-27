using DashSpec.Host.Configuration;

namespace DashSpec.Host.Services.Git;

public sealed class GitCatalogSyncBackgroundService(
    DashSpecTomlRoot bootstrap,
    GitCatalogSyncService syncService,
    ILogger<GitCatalogSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalMinutes = Math.Max(1, bootstrap.CatalogGit.PullIntervalMinutes);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken).ConfigureAwait(false);
                if (!syncService.IsEnabled)
                {
                    continue;
                }

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
                logger.LogWarning(ex, "Git catalog pull failed; will retry in {Minutes} min", intervalMinutes);
            }
        }
    }
}
