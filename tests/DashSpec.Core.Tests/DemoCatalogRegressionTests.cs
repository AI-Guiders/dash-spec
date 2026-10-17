using DashSpec.Core.Layout;
using DashSpec.Core.Parsing;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;
using Xunit;

namespace DashSpec.Core.Tests;

/// <summary>
/// demo catalog specs: parse, FilterPlacement/interior validation, multi-slot regressions.
/// Catches failures like events_detail layout token 'events_table' before Host load.
/// </summary>
public sealed class DemoCatalogRegressionTests
{
    /// <summary>Repo-relative sample root: the test host runs from tests/&lt;project&gt;/bin/&lt;cfg&gt;/&lt;tfm&gt; (five levels up).</summary>
    private static readonly string SamplesDemo = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "samples", "demo"));

    /// <summary>
    /// Standalone dashboard specs. <c>demo-analytics.ds</c> is a tab unit (ADR-0101): its cards bind filters owned by
    /// the dashboard, so it is parsed through <c>demo-soak.ds</c> (<c>import tabs from Demo.Analytics</c>), not on its own.
    /// </summary>
    private static readonly string[] CatalogFileNames =
    [
        "demo-soak.ds",
    ];

    private static DashSpecParseOptions DemoParseOptions { get; } = new()
    {
        ExtensionBlockKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "views" },
        ExtensionBlockPluginIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["views"] = "card_views",
        },
    };

    public static TheoryData<string> CatalogSpecPaths
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var name in CatalogFileNames)
            {
                data.Add(Path.Combine(SamplesDemo, name));
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CatalogSpecPaths))]
    public void Parse_catalog_spec_runs_validation_and_resolves_interior_layout(string path)
    {
        // Catalog specs must be B2 on disk (infer + rows R); no test-time datasource rewrite.
        var doc = DashSpecParser
            .Compile(File.ReadAllText(path), Path.GetDirectoryName(path)!, DemoParseOptions)
            .Document;
        Assert.NotEmpty(doc.Filters);
        Assert.NotEmpty(doc.Cards);

        foreach (var card in doc.Cards)
        {
            if (card.InteriorBoard is null)
            {
                continue;
            }

            var placements = CardInteriorLayoutCompactor.Compact(card, doc.Filters, doc.Layout.Columns);
            Assert.NotEmpty(placements);

            if (card.DiagramSlots is { Count: > 0 })
            {
                foreach (var slotRef in card.DiagramSlots.Keys)
                {
                    Assert.True(
                        placements.ContainsKey(slotRef),
                        $"Card '{card.Id}' in '{path}': layout must place diagram slot '{slotRef}'.");
                }
            }
        }
    }

    /// <summary>
    /// Regression: diagram ref with a registry diagram id imported from a unit (ADR-0098/ADR-0101),
    /// plain data block (no data for), interior [ events_table ].
    /// </summary>
    [Fact]
    public void Parse_events_detail_registry_diagram_ref_plain_data_tab()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module tab detail
            namespace Demo.Detail

            import diagrams from Demo.Diagrams
            import flows from Demo.Flows as demo_report_flow
            import types from Demo.Types

            configuration
              sqldialect = tsql
            end configuration

            connect
              use flow demo_report_flow
            end connect

            report
              title = "Детализация"
              defaults
                filter.events_top.limit = 500
                filter.usage_date.range = -7d..today
              end defaults
              filter events_top
                bind top
                end bind
                show
                  label = "Строк (TOP, 0 = все)"
                end show
              end filter
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Дата"
                end show
              end filter
              filters dashboard
                usage_date
              end dashboard
              card events_detail as "Детализация событий"
                show flow
                  events_top -> [toolbar] chrome.card.events_detail
                end show flow
                data flow
                  events_tz [rows] -> [rows] events_table
                  usage_date -> [usage_date] events_table
                end data flow
                view
                  diagram ref events_table demo_events_detail_table
                end view
                layout
                  [ events_top ]
                  [ events_table ]
                end layout
              end card
            end report
            end tab
            """,
            SamplesDemo,
            DemoParseOptions);

        var card = doc.Cards.Single(c => string.Equals(c.Id, "events_detail", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("events_table", card.DiagramSlots!.Keys);
        _ = CardInteriorLayoutCompactor.Compact(card, doc.Filters, doc.Layout.Columns);
    }

}
