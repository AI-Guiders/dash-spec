using DashSpec.Core.Model;
using DashSpec.Core.Resolution;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class ResolutionLintTests
{
    private static DashboardDocument Doc(string title, string? tabLabel = null, string? cardTitle = null) =>
        new(
            "r",
            title,
            "sqlserver",
            SqlDialect.TSql,
            null,
            null,
            null,
            LayoutDefinition.Default,
            FiltersChromeDefinition.Default,
            [],
            [],
            [new TabDefinition("t", tabLabel, ["c"])],
            [
                new CardDefinition(
                    "c",
                    cardTitle ?? "",
                    new DiagramDefinition("bar", new Dictionary<string, string>()),
                    new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
                    [],
                    []),
            ]);

    [Fact]
    public void CatalogProd_warns_when_report_and_entry_titles_both_set()
    {
        var entry = new CatalogEntryDefinition("c", "Entry Title", "c.dashspec");
        var findings = ResolutionLint.Analyze(Doc("Report Title"), entry);
        Assert.Contains(findings, f =>
            f.Slot == "report.header_title"
            && f.Mode == ResolutionMode.CatalogProd
            && f.WinnerSource == "report.title");
    }

    [Fact]
    public void DashboardEmbed_warns_entry_title_over_report_title()
    {
        var entry = new CatalogEntryDefinition("c", "Entry Title", "c.dashspec");
        var findings = ResolutionLint.Analyze(Doc("Report Title"), entry);
        Assert.Contains(findings, f =>
            f.Slot == "report.header_title"
            && f.Mode == ResolutionMode.DashboardEmbed
            && f.WinnerSource == "catalog.entry.title");
    }

    [Fact]
    public void Embed_warns_tab_title_shadowed_by_entry_title()
    {
        var entry = new CatalogEntryDefinition("c", "Entry Title", "c.dashspec");
        var findings = ResolutionLint.Analyze(Doc("Report", tabLabel: "Tab Title"), entry);
        Assert.Contains(findings, f => f.Slot == "tab.label" && f.Mode == ResolutionMode.DashboardEmbed);
    }

    [Fact]
    public void Composition_warns_legacy_diagram_chrome_with_card_presentation()
    {
        var card = new CardDefinition(
            "c",
            "",
            new DiagramDefinition("bar", new Dictionary<string, string> { ["legend"] = "bottom" }),
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            [],
            Presentation: new PresentationBlock(null, new Dictionary<string, string> { ["legend"] = "top" }));
        var doc = Doc("x") with { Cards = [card] };
        var findings = ResolutionLint.Analyze(doc);
        Assert.Contains(findings, f => f.Slot == "diagram.chart_chrome");
    }

    [Fact]
    public void Placement_information_when_place_and_board_both_set()
    {
        var card = new CardDefinition(
            "c",
            "",
            new DiagramDefinition("bar", new Dictionary<string, string>()),
            new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
            [],
            [],
            Placement: new PlacementDefinition(2, 1, 6));
        var board = LayoutBoardDefinition.FromCardRows([["c"]]);
        var doc = Doc("x") with
        {
            Cards = [card],
            Tabs = [new TabDefinition("t", null, ["c"], LayoutBoard: board)],
        };

        var findings = ResolutionLint.Analyze(doc);
        Assert.Contains(findings, f =>
            f.Slot == "tab.card_placement"
            && f.Severity == ResolutionLintSeverity.Information);
    }

    [Fact]
    public void No_findings_when_single_source_per_slot()
    {
        var entry = new CatalogEntryDefinition("c", "Entry only", "c.dashspec");
        var findings = ResolutionLint.Analyze(Doc(""), entry);
        Assert.DoesNotContain(findings, f => f.Slot == "report.header_title");
    }
}
