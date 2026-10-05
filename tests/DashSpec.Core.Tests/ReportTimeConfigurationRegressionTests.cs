using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

/// <summary>
/// Regression for ADR-0069 / DASHSPEC-ADR-0069: <c>work_time_column</c> and siblings in <c>configuration</c>.
/// </summary>
public sealed class ReportTimeConfigurationRegressionTests
{
    private const string DocsOverview =
        @"samples/demo\demo-overview.dashspec";

    /// <summary>Minimal @tab reproduction when demo docs path is absent (CI).</summary>
    private const string EmbeddedOverviewConfigurationTab = """
        @tab overview
          configuration
            sqldialect = tsql
            palette = "palettes/demo-apps.dashpalette"
            work_time_column = bucket_start_utc
          end configuration
          report
          title = "Overview"
          card c as "C"
          diagram number
          value = kpi
          end number
          datasource infer view demo.v rows FixtureRow
          end card
          end report
        end tab
        """;

    private static DashSpecParseOptions TabParseOptions { get; } = new()
    {
        MergeReferencedTabModules = false,
    };

    [Fact]
    public void Document_parser_maps_work_time_column_from_configuration()
    {
        var document = DashSpecTestRowTypes.ParseDashboard(EmbeddedOverviewConfigurationTab, specDirectory: null, TabParseOptions);

        Assert.NotNull(document.TimePolicy);
        Assert.Equal(ReportTimeBasis.Calendar, document.TimePolicy!.Basis);
        Assert.Equal("bucket_start_utc", document.TimePolicy!.WorkTimeColumn);
    }

    [Fact]
    public void Parse_demo_overview_file_when_present_accepts_work_time_column()
    {
        if (!File.Exists(DocsOverview))
        {
            return;
        }

        var options = new DashSpecParseOptions
        {
            ExtensionBlockKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "views" },
            ExtensionBlockPluginIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["views"] = "card_views",
            },
            MergeReferencedTabModules = true,
        };

        var doc = DashSpecTestRowTypes.ParseDashboard(
            File.ReadAllText(DocsOverview),
            Path.GetDirectoryName(DocsOverview)!,
            options);

        Assert.Equal("bucket_start_utc", doc.TimePolicy?.WorkTimeColumn);
    }
}
