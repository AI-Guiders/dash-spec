using DashSpec.Core.Model;
using DashSpec.Core.Resolution;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DisplayResolutionTests
{
    private static readonly DashboardDocument SampleCatalogDoc = DocumentTestData.Create(
        "customer_reports",
        "Report DSL Title",
        "sqlserver",
        SqlDialect.TSql,
        null,
        null,
        null,
        LayoutDefinition.Default,
        TestModelDefaults.FiltersChrome,
        [],
        [],
        [new TabDefinition(
            "customer_reports",
            Microsoft.FSharp.Core.FSharpOption<string>.Some("Tab Label"),
            ["peak_by_app"],
            Microsoft.FSharp.Core.FSharpOption<string>.None,
            Microsoft.FSharp.Core.FSharpOption<LayoutBoardDefinition>.None)],
        [
            CardTestData.Create(
                "peak_by_app",
                "",
                new DiagramDefinition("bar", new Dictionary<string, string>(), null),
                DashSpec.Core.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, "dbo.t"),
                [],
                []),
            CardTestData.Create(
                "detail_card",
                "Detail slice",
                new DiagramDefinition("bar", new Dictionary<string, string>(), null),
                DashSpec.Core.Parsing.DataSourceDefinitionFactory.Create(DataSourceKind.View, "dbo.t"),
                [],
                []),
        ]);

    private static readonly CatalogEntryDefinition SampleCatalogEntry = new(
        "peak_by_app",
        "Peak by app (catalog)",
        "demo-peak-by-app.dashspec",
        null,
        null);

    [Fact]
    public void CatalogProd_report_header_prefers_report_title_over_entry()
    {
        var context = ResolutionContext.ForCatalog(SampleCatalogDoc, SampleCatalogEntry);
        Assert.Equal("Report DSL Title", DisplayResolution.ResolveReportHeaderTitle(context));
    }

    [Fact]
    public void CatalogProd_report_header_falls_back_to_entry_title()
    {
        var doc = SampleCatalogDoc.WithTitle("");
        var context = ResolutionContext.ForCatalog(doc, SampleCatalogEntry);
        Assert.Equal("Peak by app (catalog)", DisplayResolution.ResolveReportHeaderTitle(context));
    }

    [Fact]
    public void CatalogProd_card_chrome_suppressed_when_id_matches_entry()
    {
        var context = ResolutionContext.ForCatalog(SampleCatalogDoc, SampleCatalogEntry);
        var card = SampleCatalogDoc.Cards[0];
        var resolved = DisplayResolution.ResolveCardChrome(context, card);
        Assert.Equal("peak_by_app", resolved.Title);
        Assert.False(resolved.ShowChromeTitle);
    }

    [Fact]
    public void CatalogProd_card_chrome_shown_for_non_entry_id()
    {
        var context = ResolutionContext.ForCatalog(SampleCatalogDoc, SampleCatalogEntry);
        var card = SampleCatalogDoc.Cards[1];
        var resolved = DisplayResolution.ResolveCardChrome(context, card);
        Assert.Equal("Detail slice", resolved.Title);
        Assert.True(resolved.ShowChromeTitle);
    }

    [Fact]
    public void SoakDev_card_chrome_uses_card_title()
    {
        var context = ResolutionContext.ForSoak(SampleCatalogDoc);
        var card = SampleCatalogDoc.Cards[1];
        var resolved = DisplayResolution.ResolveCardChrome(context, card);
        Assert.Equal("Detail slice", resolved.Title);
        Assert.True(resolved.ShowChromeTitle);
    }

    [Fact]
    public void SoakDev_report_header_uses_report_title()
    {
        var context = ResolutionContext.ForSoak(SampleCatalogDoc, SampleCatalogDoc.Tabs[0]);
        Assert.Equal("Report DSL Title", DisplayResolution.ResolveReportHeaderTitle(context));
    }

    [Fact]
    public void DashboardEmbed_card_chrome_falls_back_to_entry_title()
    {
        var card = SampleCatalogDoc.Cards[0].WithTitle("");
        var context = ResolutionContext.ForEmbed(SampleCatalogDoc, SampleCatalogEntry);
        var resolved = DisplayResolution.ResolveCardChrome(context, card);
        Assert.Equal("Peak by app (catalog)", resolved.Title);
        Assert.True(resolved.ShowChromeTitle);
    }

    [Fact]
    public void Card_chrome_title_hidden_when_chrome_requests_it()
    {
        var chrome = new CardChromeDefinition
        {
            BoundFilters = CardBoundFilterChrome.Hidden,
            HideTitle = true,
            Fold = CardFoldMode.None,
        };
        var card = SampleCatalogDoc.Cards[1].WithChrome(
            Microsoft.FSharp.Core.FSharpOption<CardChromeDefinition>.Some(chrome));
        var context = ResolutionContext.ForSoak(SampleCatalogDoc);
        var resolved = DisplayResolution.ResolveCardChrome(context, card);
        Assert.Equal("Detail slice", resolved.Title);
        Assert.False(resolved.ShowChromeTitle);
    }

    [Fact]
    public void Layout_group_title_resolves_from_group_block()
    {
        var group = new LayoutBoardGroupDefinition(
            "distribution",
            Microsoft.FSharp.Core.FSharpOption<string>.Some("Распределение"),
            [["a", "b"]]);
        Assert.Equal("Распределение", DisplayResolution.TryResolveLayoutGroupChromeTitle(group));
        Assert.Null(DisplayResolution.TryResolveLayoutGroupChromeTitle(
            new LayoutBoardGroupDefinition(group.Id, null, group.Rows)));
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
        var filter = FilterTestData.Create(FilterKind.Field, "project_name", "all", "project");
        Assert.Equal("project name", DisplayResolution.ResolveFilterLabel(filter));
    }

    [Fact]
    public void Filter_label_prefers_explicit_label()
    {
        var filter = FilterTestData.Create(FilterKind.Field, "project_name", "all", "project", label: "Проект");
        Assert.Equal("Проект", DisplayResolution.ResolveFilterLabel(filter));
    }

    [Fact]
    public void Catalog_entry_title_falls_back_to_id()
    {
        var entry = new CatalogEntryDefinition("overview", "", "overview.dashspec", null, null);
        Assert.Equal("overview", DisplayResolution.ResolveCatalogEntryTitle(entry));
    }

    [Fact]
    public void Tab_label_catalog_prod_prefers_entry_title_for_single_tab()
    {
        var context = ResolutionContext.ForCatalog(SampleCatalogDoc, SampleCatalogEntry, SampleCatalogDoc.Tabs[0]);
        Assert.Equal("Peak by app (catalog)", DisplayResolution.ResolveTabLabel(context));
    }

    [Fact]
    public void Tab_label_catalog_prod_uses_tab_title_for_multi_tab_hub()
    {
        TabDefinition[] soakTabs =
        [
            new TabDefinition("overview", Microsoft.FSharp.Core.FSharpOption<string>.Some("Обзор"), [], Microsoft.FSharp.Core.FSharpOption<string>.None, Microsoft.FSharp.Core.FSharpOption<LayoutBoardDefinition>.None),
            new TabDefinition("detail", Microsoft.FSharp.Core.FSharpOption<string>.Some("Детализация"), [], Microsoft.FSharp.Core.FSharpOption<string>.None, Microsoft.FSharp.Core.FSharpOption<LayoutBoardDefinition>.None),
        ];
        var soakDoc = SampleCatalogDoc.WithTabs(soakTabs);
        var soakEntry = new CatalogEntryDefinition("soak", "Demo", "demo-soak.ds", null, null);
        var context = ResolutionContext.ForCatalog(soakDoc, soakEntry, soakTabs[0]);
        Assert.Equal("Обзор", DisplayResolution.ResolveTabLabel(context));
    }

    [Fact]
    public void Page_nav_title_falls_back_to_id()
    {
        var page = new ReportPageDefinition
        {
            Id = "details",
            Title = Microsoft.FSharp.Core.FSharpOption<string>.None,
            LayoutBoard = Microsoft.FSharp.Core.FSharpOption<LayoutBoardDefinition>.None,
            TabId = Microsoft.FSharp.Core.FSharpOption<string>.Some("customer_reports"),
            ToolbarBoard = Microsoft.FSharp.Core.FSharpOption<LayoutBoardDefinition>.None,
            UsageDateDerive = Microsoft.FSharp.Core.FSharpOption<FilterDeriveDefinition>.None,
            FilterDefaults = Microsoft.FSharp.Core.FSharpOption<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>.None,
            DisplayBindings = Microsoft.FSharp.Core.FSharpOption<IReadOnlyDictionary<string, string>>.None,
            ScopeFlow = Microsoft.FSharp.Core.FSharpOption<DashSpec.Modeling.Parse.Document.ScopeFlowDefinition>.None,
        };
        Assert.Equal("details", DisplayResolution.ResolvePageNavTitle(page));
    }

    [Fact]
    public void Gate_message_returns_empty_when_missing()
    {
        Assert.Equal(string.Empty, DisplayResolution.ResolveGateMessage(null));
    }
}
