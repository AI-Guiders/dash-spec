using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

/// <summary>
/// Regression for ADR-0069 / DASHSPEC-ADR-0069: <c>work_time_column</c> and siblings in <c>configuration</c>.
/// Core <see cref="PropertySchemas.Configuration"/> must stay aligned with F# PropertySchemas (ADR-0048);
/// otherwise Host validate / LSP paths report Unknown property 'work_time_column'.
/// </summary>
public sealed class ReportTimeConfigurationRegressionTests
{
    private const string DocsOverview =
        @"d:\SSCADRepo\URSA.LicenseUsage\docs\dashspec\lus-dev-overview.dashspec";

    public static readonly string[] Adr0069ConfigurationKeys =
    [
        "time_basis",
        "time_apply",
        "work_time_column",
        "work_timezone",
        "work_start",
        "work_end",
        "work_days",
    ];

    /// <summary>Minimal @tab reproduction when URSA docs path is absent (CI).</summary>
    private const string EmbeddedOverviewConfigurationTab = """
        @tab overview
          configuration
            sqldialect = tsql
            palette = "palettes/lus-apps.dashpalette"
            work_time_column = bucket_start_utc
          end configuration
          report
          title = "Overview"
          card c as "C"
          diagram number
          value = kpi
          end number
          datasource view demo.v
          end card
          end report
        end tab
        """;

    private static DashSpecParseOptions TabParseOptions { get; } = new()
    {
        MergeReferencedTabModules = false,
    };

    [Fact]
    public void Core_configuration_schema_includes_adr_0069_report_time_keys()
    {
        var names = PropertySchemas.Configuration
            .Select(static x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in Adr0069ConfigurationKeys)
        {
            Assert.Contains(key, names);
        }
    }

    [Fact]
    public void Core_property_parser_accepts_work_time_column_in_configuration_block()
    {
        var text = """
            configuration
              sqldialect = tsql
              palette = "palettes/lus-apps.dashpalette"
              work_time_column = bucket_start_utc
            end configuration
            """;

        var reader = ParserUtilities.CreateReader(text);
        Assert.True(reader.TryKeyword("configuration"));
        var props = PropertyBlockParser.Parse(reader, PropertySchemas.Configuration, "configuration");

        Assert.Equal("bucket_start_utc", props["work_time_column"]);
    }

    [Fact]
    public void Document_parser_maps_work_time_column_from_configuration()
    {
        var document = DashSpecParser.Parse(EmbeddedOverviewConfigurationTab, specDirectory: null, TabParseOptions);

        Assert.NotNull(document.TimePolicy);
        Assert.Equal(ReportTimeBasis.Calendar, document.TimePolicy!.Basis);
        Assert.Equal("bucket_start_utc", document.TimePolicy!.WorkTimeColumn);
    }

    [Fact]
    public void Parse_lus_dev_overview_file_when_present_accepts_work_time_column()
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

        var doc = DashSpecParser.Parse(
            File.ReadAllText(DocsOverview),
            Path.GetDirectoryName(DocsOverview)!,
            options);

        Assert.Equal("bucket_start_utc", doc.TimePolicy?.WorkTimeColumn);
    }
}
