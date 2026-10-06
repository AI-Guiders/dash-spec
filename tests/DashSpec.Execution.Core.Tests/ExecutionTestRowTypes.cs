using System.Text;
using DashSpec.Core.Model;
using DashSpec.Execution.Parsing;

namespace DashSpec.Execution.Core.Tests;

internal static class ExecutionTestRowTypes
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "fixtures");

    private const string ModuleTypesIncludeLine = "!include \"query-row-types.dashtype\"";

    internal static string EnsureFixtureRowCatalog(string dashspecText)
    {
        if (!dashspecText.Contains("FixtureRow", StringComparison.Ordinal))
        {
            return dashspecText;
        }

        if (dashspecText.Contains("query-row-types", StringComparison.OrdinalIgnoreCase)
            || dashspecText.Contains("type FixtureRow", StringComparison.OrdinalIgnoreCase))
        {
            return dashspecText;
        }

        var normalized = dashspecText.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        var sb = new StringBuilder();
        var injected = false;

        foreach (var line in lines)
        {
            sb.Append(line);
            sb.Append('\n');

            if (injected)
            {
                continue;
            }

            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("@dashboard", StringComparison.Ordinal)
                && !trimmed.StartsWith("@tab", StringComparison.Ordinal))
            {
                continue;
            }

            var indent = line.Length - trimmed.Length + 2;
            sb.Append(new string(' ', indent));
            sb.AppendLine(ModuleTypesIncludeLine);
            injected = true;
        }

        return sb.ToString();
    }

    internal static void EnsureFixtureTypesOnDisk(string directory, string text)
    {
        if (!text.Contains("query-row-types", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var typesPath = Path.Combine(directory, "query-row-types.dashtype");
        if (File.Exists(typesPath))
        {
            return;
        }

        var source = Path.Combine(FixtureDir, "query-row-types.dashtype");
        Directory.CreateDirectory(directory);
        File.Copy(source, typesPath, overwrite: true);
    }

    internal static DashboardDocument Parse(string dashspecText, string? specDirectory = null)
    {
        var directory = specDirectory ?? FixtureDir;
        var text = EnsureFixtureRowCatalog(dashspecText);
        EnsureFixtureTypesOnDisk(directory, text);
        return DashSpecParser.Parse(text, directory);
    }
}
