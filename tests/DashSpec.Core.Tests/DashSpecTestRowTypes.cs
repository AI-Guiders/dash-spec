using DashSpec.Core.Model;
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
    internal static readonly string FixtureDir = Path.Combine(AppContext.BaseDirectory, "fixtures");

    internal const string ModuleTypesIncludeLine = "!include \"query-row-types.dashtype\"";

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

    private static readonly Lazy<RowTypeCatalog> SharedCatalog = new(() =>
        RowTypeCatalog.FromDocument(
            DashSpecParser.Parse(
                """
                @dashboard t
                  !include "query-row-types.dashtype"
                  report
                  title = "T"
                  end report
                end dashboard
                """,
                FixtureDir)));

    internal static RowTypeCatalog Catalog => SharedCatalog.Value;

    internal static DashboardDocument ParseDashboard(
        string dashspecText,
        string? specDirectory = null,
        DashSpecParseOptions? parseOptions = null)
    {
        var directory = specDirectory ?? FixtureDir;
        return parseOptions is null
            ? DashSpecParser.Parse(dashspecText, directory)
            : DashSpecParser.Parse(dashspecText, directory, parseOptions);
    }

    /// <summary>Report fragment with explicit inline <c>FixtureRow</c> (for short snippets).</summary>
    internal static DashboardDocument ParseReport(string innerReportBody)
    {
        var text = $"""
            @dashboard t
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
        var source = Path.Combine(FixtureDir, "query-row-types.dashtype");
        File.Copy(source, Path.Combine(specDirectory, "query-row-types.dashtype"), overwrite: true);
    }
}
