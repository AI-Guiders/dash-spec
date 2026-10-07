using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace DashSpec.Architecture.Tests;

public sealed class HostSourceGuardTests
{
    [Fact]
    public void Host_sources_respect_architecture_guard_config()
    {
        var repoRoot = RepositoryRootLocator.Find();
        var configPath = Path.Combine(AppContext.BaseDirectory, "host-source-guards.json");
        Assert.True(File.Exists(configPath), $"Missing guard config: {configPath}");

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<HostSourceGuardConfig>(json, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Invalid host-source-guards.json");

        var violations = new List<string>();
        foreach (var scope in config.Scopes)
        {
            foreach (var relativeDir in scope.Paths)
            {
                var absoluteDir = Path.Combine(repoRoot, relativeDir.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(absoluteDir))
                {
                    continue;
                }

                foreach (var extension in scope.Extensions)
                {
                    foreach (var file in Directory.EnumerateFiles(absoluteDir, $"*{extension}", SearchOption.AllDirectories))
                    {
                        var relativeFile = Path.GetRelativePath(repoRoot, file).Replace('\\', '/');

                        var text = File.ReadAllText(file);
                        foreach (var rule in scope.Forbidden)
                        {
                            if (IsAllowlisted(scope.Allowlist, relativeFile, rule.Id))
                            {
                                continue;
                            }

                            if (Regex.IsMatch(text, rule.Pattern, RegexOptions.CultureInvariant))
                            {
                                violations.Add($"{relativeFile}: {rule.Id} — {rule.Message}");
                            }
                        }
                    }
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static bool IsAllowlisted(IReadOnlyList<HostSourceGuardAllowlist> allowlist, string relativeFile, string ruleId) =>
        allowlist.Any(entry =>
            string.Equals(entry.File.Replace('\\', '/'), relativeFile, StringComparison.OrdinalIgnoreCase)
            && string.Equals(entry.Rule, ruleId, StringComparison.OrdinalIgnoreCase));

    private sealed class HostSourceGuardConfig
    {
        public List<HostSourceGuardScope> Scopes { get; set; } = [];
    }

    private sealed class HostSourceGuardScope
    {
        public string Id { get; set; } = string.Empty;
        public List<string> Paths { get; set; } = [];
        public List<string> Extensions { get; set; } = [];
        public List<HostSourceGuardRule> Forbidden { get; set; } = [];
        public List<HostSourceGuardAllowlist> Allowlist { get; set; } = [];
    }

    private sealed class HostSourceGuardRule
    {
        public string Id { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    private sealed class HostSourceGuardAllowlist
    {
        public string File { get; set; } = string.Empty;
        public string Rule { get; set; } = string.Empty;
    }
}

internal static class RepositoryRootLocator
{
    public static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "DashSpec.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("DashSpec repo root not found.");
    }
}
