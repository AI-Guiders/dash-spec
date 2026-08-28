using DashSpec.Host.Configuration;

namespace DashSpec.Host.Services.Git;

public sealed class GitCatalogSyncBackgroundService(
    DashSpecTomlRoot bootstrap,
    GitCatalogSyncService syncService,
    ILogger<GitCatalogSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var firstPoll = true;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!firstPoll)
            {
                var intervalMinutes = Math.Max(1, bootstrap.CatalogGit.PullIntervalMinutes);
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            firstPoll = false;

            if (!syncService.IsEnabled)
            {
                continue;
            }

            try
            {
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
                var intervalMinutes = Math.Max(1, bootstrap.CatalogGit.PullIntervalMinutes);
                logger.LogWarning(ex, "Git catalog pull failed; will retry in {Minutes} min", intervalMinutes);
            }
        }
    }
}
