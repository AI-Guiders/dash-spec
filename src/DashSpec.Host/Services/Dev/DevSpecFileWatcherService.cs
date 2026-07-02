using DashSpec.Host.Configuration;

namespace DashSpec.Host.Services.Dev;

public sealed class DevSpecFileWatcherService(
    IWebHostEnvironment environment,
    DashSpecHostContext hostContext,
    DevSpecReloadNotifier reloadNotifier,
    ILogger<DevSpecFileWatcherService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!environment.IsDevelopment())
        {
            return Task.CompletedTask;
        }

        var relative = hostContext.DefaultSpecRelativePath;
        if (string.IsNullOrWhiteSpace(relative))
        {
            return Task.CompletedTask;
        }

        var specPath = DashSpecBootstrap.ResolveSpecPath(environment.ContentRootPath, relative);
        if (!File.Exists(specPath))
        {
            logger.LogWarning("Dev file watcher: spec not found at {SpecPath}", specPath);
            return Task.CompletedTask;
        }

        var specDir = Path.GetDirectoryName(specPath)!;
        var watcher = new FileSystemWatcher(specDir)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };

        watcher.Changed += (_, e) => OnSpecFileChanged(e.FullPath);
        watcher.Created += (_, e) => OnSpecFileChanged(e.FullPath);
        watcher.Renamed += (_, e) => OnSpecFileChanged(e.FullPath);
        watcher.EnableRaisingEvents = true;

        stoppingToken.Register(() =>
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        });

        logger.LogInformation("Dev file watcher: watching {Directory}", specDir);
        return Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private void OnSpecFileChanged(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return;
        }

        var ext = Path.GetExtension(fullPath);
        if (!ext.Equals(".dashspec", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".toml", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".dashdiagram", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".dashpresentation", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".dashtransform", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".dashpalette", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".dashlayout", StringComparison.OrdinalIgnoreCase) &&
            !ext.Equals(".sql", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        logger.LogInformation("Dev file watcher: {File} changed, requesting reload", Path.GetFileName(fullPath));
        reloadNotifier.Notify();
    }
}
