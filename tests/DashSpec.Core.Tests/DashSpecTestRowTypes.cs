using System.Text;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;

namespace DashSpec.Core.Tests;

/// <summary>
/// ADR-0087 B2: <c>datasource infer … rows &lt;RowType&gt;</c> plus declared row types (<c>type</c> or module <c>!include</c>).
/// Legacy card datasource is migrated in spec text — not rewritten at parse time.
/// </summary>
internal static class DashSpecTestRowTypes
{
    static DashSpecTestRowTypes() => DashSpecTestCulture.Ensure();

    internal static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "fixtures");

    internal const string ModuleTypesIncludeLine = "import types from Fixtures.QueryRowTypes";
    internal const string ModuleFlowImportLine = "import flows from Fixtures.Flow as fixture_flow";

    internal const string InlineFixtureTypeBlock = """
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
          optional string segment
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

    private static readonly Lazy<RowTypeCatalog> SharedCatalog = new(() =>
        RowTypeCatalog.FromDocument(
            DashSpecParser.Parse(
                """
                module dashboard t
                  import types from Fixtures.QueryRowTypes
                  report
                  title = "T"
                  end report
                end dashboard
                """,
                FixtureDir)));

    internal static RowTypeCatalog Catalog => SharedCatalog.Value;

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
                || dashspecText.Contains("fixture_flow", StringComparison.Ordinal)
                || dashspecText.Contains("fixture_src", StringComparison.Ordinal))
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

    internal static DashboardDocument ParseDashboard(
        string dashspecText,
        string? specDirectory = null,
        DashSpecParseOptions? parseOptions = null)
    {
        var directory = specDirectory ?? FixtureDir;
        var text = EnsureFixtureRowCatalog(dashspecText);
        EnsureFixtureTypesOnDisk(directory, text);
        var document = parseOptions is null
            ? DashSpecParser.Parse(text, directory)
            : DashSpecParser.Parse(text, directory, parseOptions);
        return DashSpec.Execution.Runtime.DocumentFlowBinder.MaterializeFlowCards(document);
    }

    /// <summary>Report fragment with explicit inline <c>FixtureRow</c> (for short snippets).</summary>
    internal static DashboardDocument ParseReport(string innerReportBody)
    {
        var text = $"""
            module dashboard t
              report
              title = "T"
              {InlineFixtureTypeBlock}
              {innerReportBody}
              end report
            end dashboard
            """;
        return DashSpecParser.Parse(text, FixtureDir);
    }

    internal static CardDefinition ParseCard(string innerReportBody) => ParseReport(innerReportBody).Cards[0];

    internal static void SeedFixtureTypesDirectory(string specDirectory)
    {
        var source = Path.Combine(FixtureDir, "query-row-types.ds");
        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"Test fixture row types not found at '{source}'.");
        }

        Directory.CreateDirectory(specDirectory);
        File.Copy(source, Path.Combine(specDirectory, "query-row-types.ds"), overwrite: true);
        var toml = Path.Combine(FixtureDir, "dashspec.toml");
        if (File.Exists(toml))
        {
            File.Copy(toml, Path.Combine(specDirectory, "dashspec.toml"), overwrite: true);
        }
    }

    private static void EnsureFixtureTypesOnDisk(string directory, string text)
    {
        if (!text.Contains("QueryRowTypes", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var typesPath = Path.Combine(directory, "query-row-types.ds");
        if (!File.Exists(typesPath))
        {
            SeedFixtureTypesDirectory(directory);
        }
    }
}
