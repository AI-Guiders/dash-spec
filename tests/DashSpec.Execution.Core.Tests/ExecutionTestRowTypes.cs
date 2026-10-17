using System.Text;
using DashSpec.Execution.Parsing;

namespace DashSpec.Execution.Core.Tests;

internal static class ExecutionTestRowTypes
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "fixtures");

    private const string ModuleTypesIncludeLine = "import types from Fixtures.QueryRowTypes";
    private const string ModuleFlowImportLine = "import flows from Fixtures.Flow as fixture_flow";

    internal static string EnsureFixtureRowCatalog(string dashspecText)
    {
        if (!dashspecText.Contains("FixtureRow", StringComparison.Ordinal)
            && !dashspecText.Contains("fixture.ds", StringComparison.Ordinal)
            && !dashspecText.Contains("fixture_flow", StringComparison.Ordinal))
        {
            return dashspecText;
        }

        var needsTypes = !dashspecText.Contains("QueryRowTypes", StringComparison.OrdinalIgnoreCase)
            && !dashspecText.Contains("type FixtureRow", StringComparison.OrdinalIgnoreCase);
        var needsFlow = (dashspecText.Contains("fixture.ds", StringComparison.Ordinal)
                || dashspecText.Contains("fixture_flow", StringComparison.Ordinal))
            && !dashspecText.Contains("Fixtures.Flow", StringComparison.Ordinal);
        var hasTypesImport = dashspecText.Contains("import types from Fixtures.QueryRowTypes", StringComparison.OrdinalIgnoreCase);
        var hasConnect = dashspecText.Contains("connect", StringComparison.Ordinal);

        if (!needsTypes && !needsFlow)
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
            var indent = line.Length - trimmed.Length + 2;
            var atTypes = hasTypesImport
                && trimmed.StartsWith("import types from Fixtures.QueryRowTypes", StringComparison.OrdinalIgnoreCase);
            var atModule = trimmed.StartsWith("module dashboard", StringComparison.Ordinal)
                || trimmed.StartsWith("module tab", StringComparison.Ordinal);

            if (needsFlow && atTypes)
            {
                sb.Append(new string(' ', indent));
                sb.AppendLine(ModuleFlowImportLine);
                injected = true;
            }
            else if (atModule && (needsTypes || (needsFlow && !hasTypesImport)))
            {
                if (needsTypes)
                {
                    sb.Append(new string(' ', indent));
                    sb.AppendLine(ModuleTypesIncludeLine);
                }

                if (needsFlow && !hasTypesImport)
                {
                    sb.Append(new string(' ', indent));
                    sb.AppendLine(ModuleFlowImportLine);
                }

                injected = true;
            }

            if (injected && needsFlow && !hasConnect)
            {
                sb.Append(new string(' ', indent));
                sb.AppendLine("connect");
                sb.Append(new string(' ', indent + 2));
                sb.AppendLine("use flow fixture_flow");
                sb.Append(new string(' ', indent));
                sb.AppendLine("end connect");
            }
        }

        return sb.ToString();
    }

    internal static void EnsureFixtureTypesOnDisk(string directory, string text)
    {
        if (!text.Contains("QueryRowTypes", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var typesPath = Path.Combine(directory, "query-row-types.ds");
        if (File.Exists(typesPath))
        {
            return;
        }

        var source = Path.Combine(FixtureDir, "query-row-types.ds");
        Directory.CreateDirectory(directory);
        File.Copy(source, typesPath, overwrite: true);
        var toml = Path.Combine(FixtureDir, "dashspec.toml");
        if (File.Exists(toml))
        {
            File.Copy(toml, Path.Combine(directory, "dashspec.toml"), overwrite: true);
        }
    }

    internal static DashboardDocument Parse(string dashspecText, string? specDirectory = null)
    {
        var directory = specDirectory ?? FixtureDir;
        var text = EnsureFixtureRowCatalog(dashspecText);
        EnsureFixtureTypesOnDisk(directory, text);
        return DashSpecParser.Parse(text, directory);
    }
}
