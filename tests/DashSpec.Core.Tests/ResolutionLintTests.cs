using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class ResolutionLintTests
{
    private static DashboardDocument Doc(string title, string? tabLabel = null, string? cardTitle = null) =>
        DocumentTestData.Create(
            "r",
            title,
            "sqlserver",
            SqlDialect.TSql,
            null,
            null,
            null,
            new LayoutDefinition { Columns = 12 },
            TestModelDefaults.FiltersChrome,
            [],
            [],
            [new TabDefinition(
                "t",
                tabLabel is null ? Microsoft.FSharp.Core.FSharpOption<string>.None : Microsoft.FSharp.Core.FSharpOption<string>.Some(tabLabel),
                ["c"],
                Microsoft.FSharp.Core.FSharpOption<string>.None,
                Microsoft.FSharp.Core.FSharpOption<LayoutBoardDefinition>.None)],
            [
                CardTestData.Create(
                    "c",
                    cardTitle ?? "",
                new DiagramDefinition("bar", new Dictionary<string, string>(), null),
                    DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, "dbo.t"),
                    [],
                    []),
            ]);


    [Fact]
    public void Registry_codes_are_per_rule_not_the_shared_stamp()
    {
        // The stamping bug printed DS0571 for every rule; the registry restores the
        // documented per-rule codes (ADR-0057 P2): preset = DS0572, filter.unused = DS0573.
        var entry = new CatalogEntryDefinition("c", "Entry Title", "c.dashspec", null, null, null);
        var doc = DocumentTestData.Create(
            "r",
            "Report Title",
            "sqlserver",
            SqlDialect.TSql,
            null,
            null,
            null,
            new LayoutDefinition { Columns = 12 },
            TestModelDefaults.FiltersChrome,
            [FilterTestData.Create(FilterKind.Field, "f1", null, "f1")],
            [],
            [new TabDefinition(
                "t",
                Microsoft.FSharp.Core.FSharpOption<string>.None,
                ["c"],
                Microsoft.FSharp.Core.FSharpOption<string>.None,
                Microsoft.FSharp.Core.FSharpOption<LayoutBoardDefinition>.None)],
            [
                CardTestData.Create(
                    "c",
                    "",
                    new DiagramDefinition("bar", new Dictionary<string, string>(), null),
                    DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, "dbo.t"),
                    [],
                    []),
            ]);

        var findings = ResolutionLint.Analyze(doc, entry);

        Assert.Contains(findings, f => f.Slot == "report.header_title" && f.Code == "DS0571");
        Assert.Contains(findings, f => f.Slot == "filter.unused" && f.Code == "DS0573");
        Assert.DoesNotContain(findings, f => f.Slot == "filter.unused" && f.Code == "DS0571");
    }

    [Fact]
    public void Demo_corpus_is_clean_of_flow_and_type_lints()
    {
        var path = Path.Combine(
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..")),
            "samples", "demo", "demo-soak.ds");
        if (!File.Exists(path))
        {
            return; // sample tree absent in trimmed packaging
        }

        var diagnostics = DashSpec.Execution.Compilation.DashSpecDiagnosticService.ValidateFile(path);

        // The demo corpus row types cover the diagram bindings and every filtered card
        // is wired: the two lints (DS0580/DS0581) must stay silent on a clean corpus.
        Assert.DoesNotContain(diagnostics, d =>
            d.Message.Contains("DS0580 flow.unfiltered", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d =>
            d.Message.Contains("DS0581 diagram.column_not_in_type", StringComparison.Ordinal));
    }

    [Fact]
    public void CatalogProd_warns_when_report_and_entry_titles_both_set()
    {
        var entry = new CatalogEntryDefinition("c", "Entry Title", "c.dashspec", null, null, null);
        var findings = ResolutionLint.Analyze(Doc("Report Title"), entry);
        Assert.Contains(findings, f =>
            f.Slot == "report.header_title"
            && f.Mode == ResolutionMode.CatalogProd
            && f.WinnerSource == "report.title");
    }

    [Fact]
    public void DashboardEmbed_warns_entry_title_over_report_title()
    {
        var entry = new CatalogEntryDefinition("c", "Entry Title", "c.dashspec", null, null, null);
        var findings = ResolutionLint.Analyze(Doc("Report Title"), entry);
        Assert.Contains(findings, f =>
            f.Slot == "report.header_title"
            && f.Mode == ResolutionMode.DashboardEmbed
            && f.WinnerSource == "catalog.entry.title");
    }

    [Fact]
    public void Embed_warns_tab_title_shadowed_by_entry_title()
    {
        var entry = new CatalogEntryDefinition("c", "Entry Title", "c.dashspec", null, null, null);
        var findings = ResolutionLint.Analyze(Doc("Report", tabLabel: "Tab Title"), entry);
        Assert.Contains(findings, f => f.Slot == "tab.label" && f.Mode == ResolutionMode.DashboardEmbed);
    }

    [Fact]
    public void Composition_warns_legacy_diagram_chrome_with_card_presentation()
    {
        var card = CardTestData.Create(
            "c",
            "",
            new DiagramDefinition("bar", new Dictionary<string, string> { ["legend"] = "bottom" }, null),
            DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, "dbo.t"),
            [],
            [],
            Presentation: new PresentationBlock(null, new Dictionary<string, string> { ["legend"] = "top" }));
        var doc = Doc("x").WithCards([card]);
        var findings = ResolutionLint.Analyze(doc);
        Assert.Contains(findings, f => f.Slot == "diagram.chart_chrome");
    }

    [Fact]
    public void Placement_information_when_place_and_board_both_set()
    {
        var card = CardTestData.Create(
            "c",
            "",
            new DiagramDefinition("bar", new Dictionary<string, string>(), null),
            DashSpec.Execution.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, "dbo.t"),
            [],
            [],
            Placement: new PlacementDefinition(2, 1, 6));
        var board = LayoutBoardDefinition.FromCardRows([["c"]]);
        var doc = Doc("x")
            .WithCards([card])
            .WithTabs(
            [
                new TabDefinition(
                    "t",
                    Microsoft.FSharp.Core.FSharpOption<string>.None,
                    ["c"],
                    Microsoft.FSharp.Core.FSharpOption<string>.None,
                    Microsoft.FSharp.Core.FSharpOption<LayoutBoardDefinition>.Some(board)),
            ]);

        var findings = ResolutionLint.Analyze(doc);
        Assert.Contains(findings, f =>
            f.Slot == "tab.card_placement"
            && f.Severity == ResolutionLintSeverity.Information);
    }

    [Fact]
    public void No_findings_when_single_source_per_slot()
    {
        var entry = new CatalogEntryDefinition("c", "Entry only", "c.dashspec", null, null, null);
        var findings = ResolutionLint.Analyze(Doc(""), entry);
        Assert.DoesNotContain(findings, f => f.Slot == "report.header_title");
    }
}
