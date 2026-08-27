using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DashSpec.Core.Parsing;
using DashSpec.Host.Configuration;
using DashSpec.Host.Services.Dev;

namespace DashSpec.Host.Services.Git;

public sealed class GitCatalogSyncService(
    DashSpecTomlRoot bootstrap,
    CatalogSourceState catalogState,
    IWebHostEnvironment environment,
    DevSpecReloadNotifier reloadNotifier,
    ILogger<GitCatalogSyncService> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private byte[] _lastCatalogHash = TryReadBytes(catalogState.Current.FullPath);

    public bool IsEnabled =>
        bootstrap.CatalogGit.Enabled && !string.IsNullOrWhiteSpace(bootstrap.CatalogGit.Url);

    public async Task<GitCatalogSyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return GitCatalogSyncResult.Disabled();
        }

        if (!await _gate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return GitCatalogSyncResult.Busy();
        }

        try
        {
            var cacheDir = GitCatalogSynchronizer.ResolveCacheDirectory(
                bootstrap.CatalogGit,
                environment.ContentRootPath);
            var catalogPath = await Task.Run(
                    () => GitCatalogSynchronizer.SyncRepository(bootstrap.CatalogGit, cacheDir, logger),
                    cancellationToken)
                .ConfigureAwait(false);

            var bytes = await File.ReadAllBytesAsync(catalogPath, cancellationToken).ConfigureAwait(false);
            var changed = !bytes.AsSpan().SequenceEqual(_lastCatalogHash);
            if (changed)
            {
                _lastCatalogHash = bytes;
                var document = CatalogParser.ParseFile(catalogPath);
                catalogState.Replace(new CatalogBootstrap(document, catalogPath));
                logger.LogInformation("Git catalog updated, requesting reload: {Path}", catalogPath);
                reloadNotifier.Notify();
            }

            return GitCatalogSyncResult.Ok(changed, catalogPath);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static byte[] TryReadBytes(string? path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path)
                ? File.ReadAllBytes(path)
                : [];
        }
        catch
        {
            return [];
        }
    }
}

public sealed record GitCatalogSyncResult(string Status, bool Changed, string? CatalogPath, int HttpStatus)
{
    public static GitCatalogSyncResult Ok(bool changed, string catalogPath) =>
        new("ok", changed, catalogPath, StatusCodes.Status200OK);

    public static GitCatalogSyncResult Busy() =>
        new("busy", false, null, StatusCodes.Status202Accepted);

    public static GitCatalogSyncResult Disabled() =>
        new("disabled", false, null, StatusCodes.Status503ServiceUnavailable);
}
