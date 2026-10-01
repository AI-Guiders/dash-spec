using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace DashSpec.Architecture.Tests;

/// <summary>ADR-0077: inline Cyrillic in Host UI razors is frozen; growth requires operator-approved baseline update.</summary>
public sealed class HostUiCopyBaselineTests
{
    private static readonly Regex CyrillicLine = new(@"[\u0400-\u04FF]", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    [Fact]
    public void Host_ui_razors_do_not_grow_inline_cyrillic_without_baseline_update()
    {
        var repoRoot = RepositoryRootLocator.Find();
        var configPath = Path.Combine(AppContext.BaseDirectory, "host-ui-cyrillic-baseline.json");
        Assert.True(File.Exists(configPath), $"Missing baseline: {configPath}");

        var json = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<HostUiCyrillicBaselineConfig>(json, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("Invalid host-ui-cyrillic-baseline.json");

        var hostRoot = Path.Combine(repoRoot, "src", "DashSpec.Host");
        var observed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var scanRoot in config.ScanRoots)
        {
            var absoluteDir = Path.Combine(repoRoot, scanRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(absoluteDir))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(absoluteDir, "*.razor", SearchOption.AllDirectories))
            {
                var relativeFile = Path.GetRelativePath(hostRoot, file).Replace('\\', '/');
                var lineCount = CountCyrillicLines(File.ReadAllLines(file));
                if (lineCount > 0)
                {
                    observed[relativeFile] = lineCount;
                }
            }
        }

        var violations = new List<string>();

        foreach (var (file, count) in observed)
        {
            if (!config.Files.TryGetValue(file, out var allowed))
            {
                violations.Add(
                    $"{file}: {count} line(s) with Cyrillic — new inline copy is forbidden (ADR-0077). " +
                    "Use DashboardLocalizer.T(\"English key\") or escalate to operator.");
                continue;
            }

            if (count > allowed)
            {
                violations.Add(
                    $"{file}: Cyrillic lines {count} > baseline {allowed}. " +
                    "Do not shorten or invent copy in razor; add keys to DashboardLocalizer and shrink baseline after migration, or escalate.");
            }
            else if (count < allowed)
            {
                violations.Add(
                    $"{file}: Cyrillic lines {count} < baseline {allowed}. " +
                    "Update architecture/host-ui-cyrillic-baseline.json in the same PR (migration win).");
            }
        }

        foreach (var (file, allowed) in config.Files)
        {
            if (!observed.ContainsKey(file))
            {
                violations.Add(
                    $"{file}: baseline expects Cyrillic but file has none. Remove or zero the baseline entry.");
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    private static int CountCyrillicLines(IEnumerable<string> lines)
    {
        var count = 0;
        foreach (var line in lines)
        {
            if (CyrillicLine.IsMatch(line))
            {
                count++;
            }
        }

        return count;
    }

    private sealed class HostUiCyrillicBaselineConfig
    {
        public List<string> ScanRoots { get; set; } = [];
        public Dictionary<string, int> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
