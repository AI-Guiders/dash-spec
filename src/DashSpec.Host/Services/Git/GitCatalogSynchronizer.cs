using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using DashSpec.Host.Configuration;
using DashSpec.Host.Services.Abstractions;

namespace DashSpec.Host.Services.Git;

/// <summary>Clone/pull git-репозитория со specs и возврат пути к .dashcatalog.</summary>
public sealed class GitCatalogSynchronizer(ILogger<GitCatalogSynchronizer> logger) : IGitCatalogSynchronizer
{
    /// <inheritdoc />
    public bool PrepareDeferredSync(DashSpecTomlRoot bootstrap)
    {
        if (!bootstrap.CatalogGit.Enabled || string.IsNullOrWhiteSpace(bootstrap.CatalogGit.Url))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(bootstrap.CatalogGit.Path))
        {
            throw new InvalidOperationException("catalog_git.path is required when catalog_git.enabled = true.");
        }

        logger.LogInformation(
            "Git catalog configured ({Url}); Host starts on .dashhost catalog until sync succeeds.",
            bootstrap.CatalogGit.Url);
        return true;
    }

    /// <inheritdoc />
    public bool TryApply(DashSpecTomlRoot bootstrap, string contentRoot)
    {
        if (!PrepareDeferredSync(bootstrap))
        {
            return false;
        }

        var git = bootstrap.CatalogGit;
        var cacheDir = ResolveCacheDirectory(git, contentRoot);
        var catalogFullPath = SyncRepository(git, cacheDir);
        bootstrap.Dashboard.CatalogPath = catalogFullPath;
        return true;
    }

    /// <inheritdoc />
    public string SyncRepository(CatalogGitTomlSection git, string cacheDir)
    {
        Directory.CreateDirectory(cacheDir);
        var repoUrl = BuildAuthenticatedUrl(git);
        var branch = string.IsNullOrWhiteSpace(git.Branch) ? "main" : git.Branch.Trim();

        if (!Directory.Exists(Path.Combine(cacheDir, ".git")))
        {
            logger.LogInformation("Git catalog: cloning {Url} → {Dir}", git.Url, cacheDir);
            RunGit($"clone --branch {Quote(branch)} --single-branch {Quote(repoUrl)} {Quote(cacheDir)}");
        }
        else
        {
            logger.LogInformation("Git catalog: pulling {Branch} in {Dir}", branch, cacheDir);
            RunGit($"-C {Quote(cacheDir)} fetch origin {Quote(branch)}");
            RunGit($"-C {Quote(cacheDir)} reset --hard FETCH_HEAD");
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

    /// <inheritdoc />
    public string ResolveCacheDirectory(CatalogGitTomlSection git, string contentRoot)
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

    private void RunGit(string arguments)
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
            logger.LogDebug("git: {Output}", stdout.Trim());
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
