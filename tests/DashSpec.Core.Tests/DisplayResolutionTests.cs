using DashSpec.Core.Model;
using DashSpec.Core.Resolution;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DisplayResolutionTests
{
    private static readonly DashboardDocument StakeholderDoc = new(
        "stakeholder",
        "Report DSL Title",
        "sqlserver",
        SqlDialect.TSql,
        null,
        null,
        null,
        LayoutDefinition.Default,
        FiltersChromeDefinition.Default,
        [],
        [],
        [new TabDefinition("stakeholder", "Tab Label", ["peak_by_app"])],
        [
            new CardDefinition(
                "peak_by_app",
                "",
                new DiagramDefinition("bar", new Dictionary<string, string>()),
                new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
                [],
                []),
            new CardDefinition(
                "detail_card",
                "Detail slice",
                new DiagramDefinition("bar", new Dictionary<string, string>()),
                new DataSourceDefinition(DataSourceKind.View, "dbo.t"),
                [],
                []),
        ]);

    private static readonly CatalogEntryDefinition StakeholderEntry = new(
        "peak_by_app",
        "№1 Пик одновременности по ПО",
        "lus-stakeholder-peak-by-app.dashspec");

    [Fact]
    public void CatalogProd_report_header_prefers_report_title_over_entry()
    {
        var context = ResolutionContext.ForCatalog(StakeholderDoc, StakeholderEntry);
        Assert.Equal("Report DSL Title", DisplayResolution.ResolveReportHeaderTitle(context));
    }

    [Fact]
    public void CatalogProd_report_header_falls_back_to_entry_title()
    {
        var doc = StakeholderDoc with { Title = "" };
        var context = ResolutionContext.ForCatalog(doc, StakeholderEntry);
        Assert.Equal("№1 Пик одновременности по ПО", DisplayResolution.ResolveReportHeaderTitle(context));
    }

    [Fact]
    public void CatalogProd_card_chrome_suppressed_when_id_matches_entry()
    {
        var context = ResolutionContext.ForCatalog(StakeholderDoc, StakeholderEntry);
        var card = StakeholderDoc.Cards[0];
        var resolved = DisplayResolution.ResolveCardChrome(context, card);
        Assert.Equal("peak_by_app", resolved.Title);
        Assert.False(resolved.ShowChromeTitle);
    }

    [Fact]
    public void CatalogProd_card_chrome_shown_for_non_entry_id()
    {
        var context = ResolutionContext.ForCatalog(StakeholderDoc, StakeholderEntry);
        var card = StakeholderDoc.Cards[1];
        var resolved = DisplayResolution.ResolveCardChrome(context, card);
        Assert.Equal("Detail slice", resolved.Title);
        Assert.True(resolved.ShowChromeTitle);
    }

    [Fact]
    public void SoakDev_card_chrome_uses_card_title()
    {
        var context = ResolutionContext.ForSoak(StakeholderDoc);
        var card = StakeholderDoc.Cards[1];
        var resolved = DisplayResolution.ResolveCardChrome(context, card);
        Assert.Equal("Detail slice", resolved.Title);
        Assert.True(resolved.ShowChromeTitle);
    }

    [Fact]
    public void SoakDev_report_header_uses_report_title()
    {
        var context = ResolutionContext.ForSoak(StakeholderDoc, StakeholderDoc.Tabs[0]);
        Assert.Equal("Report DSL Title", DisplayResolution.ResolveReportHeaderTitle(context));
    }

    [Fact]
    public void DashboardEmbed_card_chrome_falls_back_to_entry_title()
    {
        var card = StakeholderDoc.Cards[0] with { Title = "" };
        var context = ResolutionContext.ForEmbed(StakeholderDoc, StakeholderEntry);
        var resolved = DisplayResolution.ResolveCardChrome(context, card);
        Assert.Equal("№1 Пик одновременности по ПО", resolved.Title);
        Assert.True(resolved.ShowChromeTitle);
    }

    [Fact]
    public void Layout_group_title_resolves_from_group_block()
    {
        var group = new LayoutBoardGroupDefinition(
            "distribution",
            "Распределение",
            [["a", "b"]]);
        Assert.Equal("Распределение", DisplayResolution.TryResolveLayoutGroupChromeTitle(group));
        Assert.Null(DisplayResolution.TryResolveLayoutGroupChromeTitle(group with { Title = null }));
    }

    [Fact]
    public void InferMode_catalog_when_entry_active()
    {
        Assert.Equal(ResolutionMode.CatalogProd, DisplayResolution.InferMode("peak_by_app", true));
        Assert.Equal(ResolutionMode.SoakDev, DisplayResolution.InferMode(null, true));
        Assert.Equal(ResolutionMode.SoakDev, DisplayResolution.InferMode("peak_by_app", false));
    }

    [Fact]
    public void Filter_label_humanizes_name_when_label_missing()
    {
        var filter = new FilterDefinition(FilterKind.Field, "project_name", "all", "project");
        Assert.Equal("project name", DisplayResolution.ResolveFilterLabel(filter));
    }

    [Fact]
    public void Filter_label_prefers_explicit_label()
    {
        var filter = new FilterDefinition(FilterKind.Field, "project_name", "all", "project", Label: "Проект");
        Assert.Equal("Проект", DisplayResolution.ResolveFilterLabel(filter));
    }

    [Fact]
    public void Catalog_entry_title_falls_back_to_id()
    {
        var entry = new CatalogEntryDefinition("overview", "", "overview.dashspec");
        Assert.Equal("overview", DisplayResolution.ResolveCatalogEntryTitle(entry));
    }

    [Fact]
    public void Tab_label_catalog_prod_prefers_entry_title()
    {
        var context = ResolutionContext.ForCatalog(StakeholderDoc, StakeholderEntry, StakeholderDoc.Tabs[0]);
        Assert.Equal("№1 Пик одновременности по ПО", DisplayResolution.ResolveTabLabel(context));
    }

    [Fact]
    public void Page_nav_title_falls_back_to_id()
    {
        var page = new ReportPageDefinition("details", null, TabId: "stakeholder");
        Assert.Equal("details", DisplayResolution.ResolvePageNavTitle(page));
    }

    [Fact]
    public void Gate_message_returns_empty_when_missing()
    {
        Assert.Equal(string.Empty, DisplayResolution.ResolveGateMessage(null));
    }
}
