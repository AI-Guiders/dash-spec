using System.Text.RegularExpressions;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;

namespace DashSpec.Core.Tests;

internal static class DashSpecTestRowTypes
{
    private static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "fixtures");

    private static readonly Lazy<RowTypeCatalog> SharedCatalog = new(() =>
        RowTypeCatalog.FromDocument(
            ParseDashboard("""
                @dashboard t
                  report
                  title = "T"
                  card c as "C"
                  diagram bar
                  x = a
                  y = b
                  end bar
                  datasource infer view dbo.t rows FixtureRow
                  end card
                  end report
                end dashboard
                """)));

    internal static RowTypeCatalog Catalog => SharedCatalog.Value;

    internal const string InlineFixtureType = """
        type FixtureRow
          optional date usage_date
          optional string app_name
          optional string x
          optional string y
          optional string a
          optional string b
          optional string user_sam
          optional decimal peak
          optional decimal peak_concurrent_proxy
          optional decimal value
          optional string category
          optional decimal utilization_pct
          optional string color
          optional string reference
          optional int count
          optional string name
          optional string label
          optional datetime occurred_at
          optional date day
          optional string series
          optional string stakeholder
          optional string product
          optional string column_a
          optional string column_b
          optional string value_col
          optional string form
          optional decimal launch_count
          optional decimal distinct_users
          optional decimal kpi
          optional string bucket_start_utc
        end type
        """;

    internal static DashboardDocument ParseDashboard(
        string dashspecText,
        string? specDirectory = null,
        DashSpecParseOptions? parseOptions = null)
    {
        if (!dashspecText.Contains("datasource", StringComparison.OrdinalIgnoreCase))
        {
            return parseOptions is null
                ? DashSpecParser.Parse(dashspecText, specDirectory)
                : DashSpecParser.Parse(dashspecText, specDirectory, parseOptions);
        }

        var text = AppendRowsSuffix(dashspecText);
        text = EnsureInlineFixtureType(text);

        return parseOptions is null
            ? DashSpecParser.Parse(text, specDirectory ?? FixtureDir)
            : DashSpecParser.Parse(text, specDirectory ?? FixtureDir, parseOptions);
    }

    internal static DashboardDocument ParseReport(string innerReportBody)
    {
        var report = AppendRowsSuffix(innerReportBody);
        var text = $"""
            @dashboard t
              report
              {InlineFixtureType}
              {report}
              end report
            end dashboard
            """;
        return DashSpecParser.Parse(text, FixtureDir);
    }

    internal static CardDefinition ParseCard(string innerReportBody) => ParseReport(innerReportBody).Cards[0];

    private static string EnsureInlineFixtureType(string text)
    {
        if (text.Contains("type FixtureRow", StringComparison.OrdinalIgnoreCase))
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

        return text.Insert(lineEnd + 1, InlineFixtureType + Environment.NewLine);
    }

    private static string AppendRowsSuffix(string text) =>
        Regex.Replace(
            text,
            @"(datasource\s+(?:view\s+\S+|sql(?:\s+query\s+""[^""]*""|\s+file\s+""[^""]*""|\s*\{[^}]*\})|xlsx\s+file\s+""[^""]*""(?:\s+sheet\s+""[^""]*"")?))(?!\s+rows\b)",
            "$1 rows FixtureRow",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);
}
