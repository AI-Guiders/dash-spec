using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class ReportPagesAndCatalogTests
{
    [Fact]
    public void Parse_report_page_assigns_page_id_to_cards()
    {
        var doc = DashSpecParser.Parse("""
            @tab t
              report
              title = "T"
              page overview
              title = "Overview"
              card a as "A"
              diagram bar
              x = a y = b
              end bar
              datasource view dbo.t
              end card
              end page
              page detail
              card b as "B"
              diagram bar
              x = a y = b
              end bar
              datasource view dbo.t
              end card
              end page
              end report
            end tab
            """);

        Assert.Equal(2, doc.Pages.Count);
        Assert.Equal("overview", doc.Cards[0].PageId);
        Assert.Equal("detail", doc.Cards[1].PageId);
        Assert.Equal("Overview", doc.Pages[0].Title);
    }

    [Fact]
    public void Parse_goto_page_and_entry()
    {
        var doc = DashSpecParser.Parse("""
            @tab t
              report
              title = "T"
              card nav as "Nav"
              on click
              goto page detail
              goto entry soak
              end click
              diagram bar
              x = a y = b
              end bar
              datasource view dbo.t
              end card
              end report
            end tab
            """);

        var effects = doc.Cards[0].ClickBehaviour!.Effects;
        Assert.IsType<GotoPageEffect>(effects[0]);
        Assert.Equal("detail", ((GotoPageEffect)effects[0]).PageId);
        Assert.IsType<GotoCatalogEntryEffect>(effects[1]);
        Assert.Equal("soak", ((GotoCatalogEntryEffect)effects[1]).EntryId);
        Assert.Null(((GotoCatalogEntryEffect)effects[1]).PreserveFilterNames);
    }

    [Fact]
    public void Parse_goto_entry_preserving_filters()
    {
        var doc = DashSpecParser.Parse("""
            @tab t
              report
              title = "T"
              card nav as "Nav"
              on click
              set user_name from y
              goto entry detail preserving filters usage_date, user_name
              end click
              diagram bar
              x = a y = b
              end bar
              datasource view dbo.t
              end card
              end report
            end tab
            """);

        var effects = doc.Cards[0].ClickBehaviour!.Effects;
        Assert.IsType<SetFilterFromFieldEffect>(effects[0]);
        var gotoEntry = Assert.IsType<GotoCatalogEntryEffect>(effects[1]);
        Assert.Equal("detail", gotoEntry.EntryId);
        Assert.Equal(["usage_date", "user_name"], gotoEntry.PreserveFilterNames);
    }

    [Fact]
    public void Parse_goto_entry_preserving_matching_filters()
    {
        var doc = DashSpecParser.Parse("""
            @tab t
              report
              title = "T"
              card nav as "Nav"
              on click
              goto entry detail preserving filters
              end click
              diagram bar
              x = a y = b
              end bar
              datasource view dbo.t
              end card
              end report
            end tab
            """);

        var gotoEntry = Assert.IsType<GotoCatalogEntryEffect>(doc.Cards[0].ClickBehaviour!.Effects[0]);
        Assert.Equal("detail", gotoEntry.EntryId);
        Assert.NotNull(gotoEntry.PreserveFilterNames);
        Assert.Empty(gotoEntry.PreserveFilterNames!);
    }

    [Fact]
    public void Parse_catalog_group_assigns_group_id()
    {
        var catalog = CatalogParser.Parse("""
            @catalog demo
            
            default soak
            
            group stakeholder
              title = "Заказчик"
              entry peak as "Peak"
              dashspec "peak.dashspec"
            end group
            
            entry soak as "Soak"
              dashspec "soak.dashspec"
            """);

        Assert.Single(catalog.Groups);
        Assert.Equal("stakeholder", catalog.Groups[0].Id);
        Assert.Equal("Заказчик", catalog.Groups[0].Title);
        Assert.Equal("stakeholder", catalog.Entries[0].GroupId);
        Assert.Null(catalog.Entries[1].GroupId);
    }

    [Fact]
    public void Parse_merged_paged_tab_module_allows_non_paged_sibling_tab()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-pages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "overview.dashspec"), """
                @tab overview
                  report
                  title = "Overview"
                  card plain as "Plain"
                  diagram bar
                  x = a y = b
                  end bar
                  datasource view dbo.t
                  end card
                  end report
                end tab
                """);

            File.WriteAllText(Path.Combine(dir, "stakeholder.dashspec"), """
                @tab stakeholder
                  report
                  title = "Stakeholder"
                  page p1
                  card paged as "Paged"
                  diagram bar
                  x = a y = b
                  end bar
                  datasource view dbo.t
                  end card
                  end page
                  end report
                end tab
                """);

            var doc = DashSpecParser.Parse("""
                @dashboard soak
                  report
                  title = "Soak"
                  tab overview dashspec "overview.dashspec"
                  tab stakeholder dashspec "stakeholder.dashspec"
                  end report
                end dashboard
                """, dir);

            Assert.Equal(2, doc.Tabs.Count);
            Assert.Single(doc.Pages);
            Assert.Equal("stakeholder", doc.Pages[0].TabId);
            Assert.Null(doc.Cards.First(c => c.Id == "plain").PageId);
            Assert.Equal("p1", doc.Cards.First(c => c.Id == "paged").PageId);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parse_layout_module_accepts_scope_page()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-page-scope-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "layouts"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "layouts", "page-grid.dashlayout"), """
                @layout page_grid
                scope page
                [ a b ]
                """);

            var doc = DashSpecParser.Parse("""
                @tab t
                  report
                  title = "T"
                  page overview
                  include layout "layouts/page-grid"
                  card a as "A"
                  diagram bar
                  x = a y = b
                  end bar
                  datasource view dbo.t
                  end card
                  card b as "B"
                  diagram bar
                  x = a y = b
                  end bar
                  datasource view dbo.t
                  end card
                  end page
                  end report
                end tab
                """, dir);

            Assert.Equal(LayoutScope.Page, doc.Pages[0].LayoutBoard!.ModuleScope);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void ValidateSpec_rejects_card_outside_page_when_pages_declared()
    {
        Assert.Throws<DashSpecParseException>(() => DashSpecParser.Parse("""
            @tab t
              report
              title = "T"
              page overview
              card a as "A"
              diagram bar
              x = a y = b
              end bar
              datasource view dbo.t
              end card
              end page
              card orphan as "Orphan"
              diagram bar
              x = a y = b
              end bar
              datasource view dbo.t
              end card
              end report
            end tab
            """));
    }
}
