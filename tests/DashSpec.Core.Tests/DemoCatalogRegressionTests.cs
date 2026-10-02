using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

/// <summary>
/// demo catalog specs (docs + publish installer): parse, FilterPlacement/interior validation, multi-slot regressions.
/// Catches failures like events_detail layout token 'events_table' before Host load.
/// </summary>
public sealed class DemoCatalogRegressionTests
{
    private const string DocsRoot = @"samples/demo";
    private const string PublishRoot = @"samples/demo";

    private static readonly string[] CatalogFileNames =
    [
        "demo-detail.dashspec",
        "demo-overview.dashspec",
        "demo-stakeholder.dashspec",
        "demo-soak.dashspec",
        "demo-versions.dashspec",
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
            foreach (var root in new[] { DocsRoot, PublishRoot })
            {
                foreach (var name in CatalogFileNames)
                {
                    data.Add(Path.Combine(root, name));
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(CatalogSpecPaths))]
    public void Parse_catalog_spec_runs_validation_and_resolves_interior_layout(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var doc = DashSpecTestRowTypes.ParseDashboard(File.ReadAllText(path), Path.GetDirectoryName(path)!, DemoParseOptions);
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
    /// Regression: diagram ref + registry id from !include, plain data block (no data for), interior [ events_table ].
    /// </summary>
    [Fact]
    public void Parse_events_detail_registry_diagram_ref_plain_data_tab()
    {
        var specDirectory = PublishRoot;
        if (!Directory.Exists(specDirectory))
        {
            return;
        }

        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @tab detail
            configuration
              sqldialect = tsql
            end configuration
            !include "diagrams/detail/events-detail-table.dashdiagram"
            report
              title = "Детализация"
              toolbar usage_date
              standalone
                defaults
                  filter.events_top.limit = 500
                  filter.usage_date.range = -7d..today
                end defaults
                filter usage_date
                  bind date
                    column = usage_date
                  end bind
                  show
                    label = "Дата"
                  end show
                end filter
                filter events_top
                  bind top
                    min = 0
                    max = 50000
                  end bind
                  show
                    label = "Строк (TOP, 0 = все)"
                    ref = events_top
                  end show
                end filter
              end standalone
              card events_detail ref events_detail
                title = "Детализация событий"
                filters events_top
                data
                  datasource view demo.v_events_detail
                  bind usage_date
                end data
                view
                  diagram ref events_table demo_events_detail_table
                end view
                layout
                  [ events_top ]
                  [ events_table ]
                end layout
              end card
            end report
            """,
            specDirectory,
            DemoParseOptions);

        var card = doc.Cards.Single(c => string.Equals(c.Id, "events_detail", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("events_table", card.DiagramSlots!.Keys);
        _ = CardInteriorLayoutCompactor.Compact(card, doc.Filters, doc.Layout.Columns);
    }

    [Fact]
    public void Parse_overview_peak_concurrent_proxy_views_and_tooltip_click()
    {
        var path = Path.Combine(DocsRoot, "demo-overview.dashspec");
        if (!File.Exists(path))
        {
            return;
        }

        var doc = DashSpecTestRowTypes.ParseDashboard(File.ReadAllText(path), Path.GetDirectoryName(path)!, DemoParseOptions);
        var card = doc.Cards.Single(c => string.Equals(c.Id, "peak_concurrent_proxy", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(card.InteriorBoard);
        Assert.NotNull(card.ExtensionBlocks);
        Assert.Contains(card.ExtensionBlocks!, b => string.Equals(b.Keyword, "views", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(card.ClickBehaviour);
        var drill = Assert.IsType<DrillTableFromCellEffect>(card.ClickBehaviour!.Effects[0]);
        Assert.Equal(2, drill.Binds.Count);
        Assert.Contains(drill.Binds, b => b.FilterName == "usage_date" && b.Field == "x");
        Assert.Contains(drill.Binds, b => b.FilterName == "app_name" && b.Field == "y");

        var placements = CardInteriorLayoutCompactor.Compact(card, doc.Filters, doc.Layout.Columns);
        Assert.Equal(2, placements.Count);
        Assert.True(placements.ContainsKey("heatmap"));
        Assert.True(placements.ContainsKey("drill"));
    }
}
