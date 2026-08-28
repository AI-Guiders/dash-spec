using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace DashSpec.Host.Configuration;

/// <summary>Clone/pull git-репозитория со specs и возврат пути к .dashcatalog.</summary>
public static class GitCatalogSynchronizer
{
    /// <summary>
    /// Applies env overrides and validates git catalog config.
    /// Does not clone/pull — deferred to <see cref="Services.Git.GitCatalogSyncService"/> (boot uses <c>[dashboard] catalog_path</c>).
    /// </summary>
    public static bool PrepareDeferredSync(DashSpecTomlRoot bootstrap, ILogger? logger = null)
    {
        ApplyCatalogGitEnvOverrides(bootstrap.CatalogGit);

        if (!bootstrap.CatalogGit.Enabled || string.IsNullOrWhiteSpace(bootstrap.CatalogGit.Url))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(bootstrap.CatalogGit.Path))
        {
            throw new InvalidOperationException("catalog_git.path is required when catalog_git.enabled = true.");
        }

        logger?.LogInformation(
            "Git catalog configured ({Url}); Host starts on [dashboard] catalog_path until sync succeeds.",
            bootstrap.CatalogGit.Url);
        return true;
    }

    /// <summary>Clone/pull and return catalog file path (on-demand / background sync).</summary>
    public static bool TryApply(DashSpecTomlRoot bootstrap, string contentRoot, ILogger? logger = null)
    {
        if (!PrepareDeferredSync(bootstrap, logger))
        {
            return false;
        }

        var git = bootstrap.CatalogGit;
        var cacheDir = ResolveCacheDirectory(git, contentRoot);
        var catalogFullPath = SyncRepository(git, cacheDir, logger);
        bootstrap.Dashboard.CatalogPath = catalogFullPath;
        return true;
    }

    public static string SyncRepository(CatalogGitTomlSection git, string cacheDir, ILogger? logger = null)
    {
        Directory.CreateDirectory(cacheDir);
        var repoUrl = BuildAuthenticatedUrl(git);
        var branch = string.IsNullOrWhiteSpace(git.Branch) ? "main" : git.Branch.Trim();

        if (!Directory.Exists(Path.Combine(cacheDir, ".git")))
        {
            logger?.LogInformation("Git catalog: cloning {Url} → {Dir}", git.Url, cacheDir);
            RunGit($"clone --branch {Quote(branch)} --single-branch {Quote(repoUrl)} {Quote(cacheDir)}", logger);
        }
        else
        {
            logger?.LogInformation("Git catalog: pulling {Branch} in {Dir}", branch, cacheDir);
            RunGit($"-C {Quote(cacheDir)} fetch origin {Quote(branch)}", logger);
            RunGit($"-C {Quote(cacheDir)} reset --hard FETCH_HEAD", logger);
        }

        var catalogFullPath = Path.GetFullPath(Path.Combine(cacheDir, git.Path.Replace('/', Path.DirectorySeparatorChar)));
        if (!File.Exists(catalogFullPath) && !catalogFullPath.EndsWith(".dashcatalog", StringComparison.OrdinalIgnoreCase))
        {
            catalogFullPath += ".dashcatalog";
        }

        if (!File.Exists(catalogFullPath))
        {
            throw new FileNotFoundException("Git catalog file not found after sync.", catalogFullPath);
        }

        return catalogFullPath;
    }

    public static string ResolveCacheDirectory(CatalogGitTomlSection git, string contentRoot)
    {
        if (!string.IsNullOrWhiteSpace(git.CacheDirectory))
        {
            return Path.GetFullPath(git.CacheDirectory);
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(git.Url.Trim())))
            .Substring(0, 16)
            .ToLowerInvariant();
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "DashSpec",
            "git-catalogs");
        return Path.Combine(baseDir, hash);
    }

    private static void ApplyCatalogGitEnvOverrides(CatalogGitTomlSection git)
    {
        var url = Environment.GetEnvironmentVariable("DASHSPEC_CATALOG_GIT_URL");
        if (!string.IsNullOrWhiteSpace(url))
        {
            git.Enabled = true;
            git.Url = url;
        }

        var branch = Environment.GetEnvironmentVariable("DASHSPEC_CATALOG_GIT_BRANCH");
        if (!string.IsNullOrWhiteSpace(branch))
        {
            git.Branch = branch;
        }

        var path = Environment.GetEnvironmentVariable("DASHSPEC_CATALOG_GIT_PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            git.Path = path;
        }

        var password = Environment.GetEnvironmentVariable("DASHSPEC_CATALOG_GIT_PASSWORD");
        if (!string.IsNullOrWhiteSpace(password))
        {
            git.Password = password;
        }

        var username = Environment.GetEnvironmentVariable("DASHSPEC_CATALOG_GIT_USERNAME");
        if (!string.IsNullOrWhiteSpace(username))
        {
            git.Username = username;
        }

        var interval = Environment.GetEnvironmentVariable("DASHSPEC_CATALOG_GIT_PULL_MINUTES");
        if (int.TryParse(interval, out var minutes) && minutes > 0)
        {
            git.PullIntervalMinutes = minutes;
        }

        var syncSecret = Environment.GetEnvironmentVariable("DASHSPEC_CATALOG_SYNC_SECRET");
        if (!string.IsNullOrWhiteSpace(syncSecret))
        {
            git.SyncWebhookSecret = syncSecret;
        }

        var syncRepo = Environment.GetEnvironmentVariable("DASHSPEC_CATALOG_SYNC_REPO_SLUG");
        if (!string.IsNullOrWhiteSpace(syncRepo))
        {
            git.SyncRepoSlug = syncRepo;
        }

        var allowUnsigned = Environment.GetEnvironmentVariable("DASHSPEC_CATALOG_SYNC_ALLOW_UNSIGNED");
        if (bool.TryParse(allowUnsigned, out var unsigned))
        {
            git.SyncAllowUnsigned = unsigned;
        }
    }

    private static string BuildAuthenticatedUrl(CatalogGitTomlSection git)
    {
        var url = git.Url.Trim();
        if (string.IsNullOrWhiteSpace(git.Username))
        {
            return url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        var password = git.Password;
        var user = git.Username.Trim();
        var builder = new UriBuilder(uri)
        {
            UserName = user,
            Password = password,
        };
        return builder.Uri.ToString();
    }

    private static void RunGit(string arguments, ILogger? logger)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start git. Install Git for Windows and ensure git is on PATH.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(TimeSpan.FromMinutes(5));

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {arguments} failed (exit {process.ExitCode}): {stderr.Trim()} {stdout.Trim()}".Trim());
        }

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            logger?.LogDebug("git: {Output}", stdout.Trim());
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
