using System.Text.RegularExpressions;
using DashSpec.Core.Model;
using DashSpec.Execution.Parsing;

namespace DashSpec.Execution.Core.Tests;

internal static class ExecutionTestRowTypes
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "fixtures");

    internal const string InlineDemoRow = """
        type DemoRow
          optional date usage_date
          optional string app_name
          optional decimal value
          optional string x
        end type
        """;

    internal static DashboardDocument Parse(string dashspecText, string? specDirectory = null)
    {
        if (!dashspecText.Contains("datasource", StringComparison.OrdinalIgnoreCase))
        {
            return DashSpecParser.Parse(dashspecText, specDirectory);
        }

        var text = Prepare(dashspecText);
        return DashSpecParser.Parse(text, specDirectory ?? FixtureDir);
    }

    private static string EnsureInlineType(string text)
    {
        if (text.Contains("type DemoRow", StringComparison.OrdinalIgnoreCase))
        {
            return text;
        }

        var reportIndex = text.IndexOf("report", StringComparison.OrdinalIgnoreCase);
        if (reportIndex < 0)
        {
            return text;
        }

        var lineEnd = text.IndexOf('\n', reportIndex);
        if (lineEnd < 0)
        {
            lineEnd = text.Length;
        }

        return text.Insert(lineEnd + 1, InlineDemoRow + Environment.NewLine);
    }

    private static string NormalizeDatasourceRows(string text)
    {
        text = Regex.Replace(
            text,
            @"\bdatasource\s+view\b",
            "datasource infer view",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        text = Regex.Replace(
            text,
            @"\s+rows\s+\w+",
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return AppendRowsSuffix(text);
    }

    private static string AppendRowsSuffix(string text) =>
        Regex.Replace(
            text,
            @"(datasource\s+(?:infer\s+)?(?:view\s+\S+|sql(?:\s+query\s+""[^""]*""|\s+file\s+""[^""]*""|\s*\{[^}]*\})|xlsx\s+file\s+""[^""]*""(?:\s+sheet\s+""[^""]*"")?))(?!\s+rows\b)",
            "$1 rows DemoRow",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

    internal static string Prepare(string text) => EnsureInlineType(NormalizeDatasourceRows(text));
}
