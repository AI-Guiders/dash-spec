#nullable enable

using System.Diagnostics;
using System.Text;

namespace DashSpec.Plugin.Studio.Services;

public sealed record StudioGitFileStatus(string RelativePath, string StatusCode);

public sealed record StudioGitCommitInfo(string Sha, string Author, string RelativeDate, string Subject);

public sealed record StudioGitOverview(
    bool IsGitRepository,
    string Branch,
    IReadOnlyList<StudioGitFileStatus> ChangedFiles,
    IReadOnlyList<StudioGitCommitInfo> RecentCommits);

/// <summary>
/// Git operations and atomic publish gate for the DashSpec catalog workspace (ADR-0133 §3.3).
/// </summary>
public sealed class StudioGitService
{
    public StudioGitOverview GetOverview(string workspaceRoot)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
        {
            return new StudioGitOverview(false, "—", [], []);
        }

        var (ok, branchOut, _) = TryRunGit(workspaceRoot, "rev-parse --abbrev-ref HEAD");
        if (!ok)
        {
            return new StudioGitOverview(false, "—", [], []);
        }

        var branch = string.IsNullOrWhiteSpace(branchOut) ? "HEAD" : branchOut.Trim();
        var (_, prefixOut, _) = TryRunGit(workspaceRoot, "rev-parse --show-prefix");
        var repoPrefix = prefixOut?.Trim().Replace('\\', '/') ?? string.Empty;

        var (_, statusOut, _) = TryRunGit(workspaceRoot, "status --porcelain .");
        var changed = new List<StudioGitFileStatus>();

        foreach (var rawLine in statusOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length < 4)
            {
                continue;
            }

            var code = line[..2].Trim();
            var path = line[3..].Trim().Trim('"').Replace('\\', '/');
            if (!string.IsNullOrEmpty(repoPrefix) &&
                path.StartsWith(repoPrefix, StringComparison.OrdinalIgnoreCase))
            {
                path = path[repoPrefix.Length..].TrimStart('/');
            }

            if (path.EndsWith(".ds", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".dsproj", StringComparison.OrdinalIgnoreCase))
            {
                changed.Add(new StudioGitFileStatus(path, string.IsNullOrEmpty(code) ? "M" : code));
            }
        }

        var (_, logOut, _) = TryRunGit(
            workspaceRoot,
            "log -n 10 --pretty=format:\"%h|%an|%ar|%s\" -- .");
        var commits = new List<StudioGitCommitInfo>();
        foreach (var rawLine in logOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = rawLine.TrimEnd('\r').Split('|', 4);
            if (parts.Length == 4)
            {
                commits.Add(new StudioGitCommitInfo(parts[0], parts[1], parts[2], parts[3]));
            }
        }

        return new StudioGitOverview(true, branch, changed, commits);
    }

    public string GetFileGitDiff(string workspaceRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
        {
            return string.Empty;
        }

        var safePath = Quote(relativePath.Replace('\\', '/'));
        var (ok, stdout, _) = TryRunGit(workspaceRoot, $"diff HEAD -- {safePath}");
        return ok ? stdout : string.Empty;
    }

    public (bool Success, string Message) CommitFiles(
        string workspaceRoot,
        IReadOnlyList<string> relativePaths,
        string authorName,
        string commitMessage)
    {
        if (relativePaths.Count == 0)
        {
            return (false, "Нет файлов для коммита.");
        }

        var cleanAuthor = string.IsNullOrWhiteSpace(authorName) ? "Studio Author" : authorName.Trim();
        var safeEmail = new string(cleanAuthor.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(safeEmail))
        {
            safeEmail = "author";
        }

        var argsBuilder = new StringBuilder("add --");
        foreach (var rel in relativePaths)
        {
            argsBuilder.Append(' ').Append(Quote(rel.Replace('\\', '/')));
        }

        var (addOk, _, addErr) = TryRunGit(workspaceRoot, argsBuilder.ToString());
        if (!addOk)
        {
            return (false, $"git add не удался: {addErr}");
        }

        var authorArg = Quote($"{cleanAuthor} <{safeEmail}@dashspec.studio>");
        var msgArg = Quote(string.IsNullOrWhiteSpace(commitMessage) ? "chore(studio): update dashspec modules" : commitMessage.Trim());
        var (commitOk, commitOut, commitErr) = TryRunGit(workspaceRoot, $"commit --author={authorArg} -m {msgArg}");
        if (!commitOk)
        {
            return (false, $"git commit не удался: {commitErr} {commitOut}".Trim());
        }

        return (true, commitOut.Trim());
    }

    public static string BuildUnifiedLineDiff(string diskText, string draftText, string relativePath)
    {
        if (string.Equals(diskText, draftText, StringComparison.Ordinal))
        {
            return "Черновик совпадает с опубликованным файлом на диске.";
        }

        var oldLines = NormalizeLines(diskText);
        var newLines = NormalizeLines(draftText);
        var sb = new StringBuilder();
        sb.AppendLine($"--- published/{relativePath}");
        sb.AppendLine($"+++ draft/{relativePath}");

        var max = Math.Max(oldLines.Length, newLines.Length);
        for (var i = 0; i < max; i++)
        {
            var oldLine = i < oldLines.Length ? oldLines[i] : null;
            var newLine = i < newLines.Length ? newLines[i] : null;
            if (oldLine == newLine)
            {
                sb.AppendLine($"  {i + 1,4}: {oldLine}");
            }
            else
            {
                if (oldLine is not null)
                {
                    sb.AppendLine($"- {i + 1,4}: {oldLine}");
                }

                if (newLine is not null)
                {
                    sb.AppendLine($"+ {i + 1,4}: {newLine}");
                }
            }
        }

        return sb.ToString();
    }

    private static string[] NormalizeLines(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    private static (bool Success, string StdOut, string StdErr) TryRunGit(string workingDir, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                return (false, string.Empty, "Не удалось запустить процесс git.");
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit(TimeSpan.FromSeconds(15));
            return (process.ExitCode == 0, stdout, stderr);
        }
        catch (Exception ex)
        {
            return (false, string.Empty, ex.Message);
        }
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
