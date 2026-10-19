using Xunit;

using DashSpec.Execution.Runtime;

namespace DashSpec.Core.Tests;

/// <summary>
/// Regression for ADR-0069 / DASHSPEC-ADR-0069: <c>work_time_column</c> and siblings in <c>configuration</c>.
/// </summary>
public sealed class ReportTimeConfigurationRegressionTests
{
    /// <summary>Minimal module tab reproduction: no demo sample sets <c>work_time_column</c> any more.</summary>
    private const string EmbeddedOverviewConfigurationTab = """
        module tab overview
          configuration
            sqldialect = tsql
            work_time_column = bucket_start_utc
          end configuration
          report
          title = "Overview"
          card c as "C"
          diagram number
          value = kpi
          end number
          data flow { fixture_src [rows] -> [rows] __diagram__ }
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
        Assert.Equal(ReportTimeBasis.Calendar, document.TimePolicy!.Value.Basis);
        Assert.Equal("bucket_start_utc", document.TimePolicy!.Value.WorkTimeColumn?.Value);
    }
}
