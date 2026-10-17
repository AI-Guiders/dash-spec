using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class ReportPagesAndCatalogTests
{
    [Fact]
    public void Parse_report_page_assigns_page_id_to_cards()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module tab t
              report
              title = "T"
              page overview
              title = "Overview"
              card a as "A"
              diagram bar
              x = a y = b
              end bar
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end page
              page detail
              card b as "B"
              diagram bar
              x = a y = b
              end bar
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end page
              end report
            end tab
            """);

        Assert.NotNull(doc.Pages);
        Assert.Equal(2, doc.Pages!.Value.Count);
        Assert.Equal("overview", doc.Cards[0].PageId);
        Assert.Equal("detail", doc.Cards[1].PageId);
        Assert.Equal("Overview", doc.Pages!.Value[0].Title);
    }

    [Fact]
    public void Parse_goto_page_and_entry()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module tab t
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
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end tab
            """);

        var effects = doc.Cards[0].ClickBehaviour!.Value.Effects;
        Assert.Equal("GotoPage", CardClickEffectTestData.CaseName(effects[0]));
        Assert.Equal("detail", CardClickEffectTestData.Fields(effects[0])[0]!);
        Assert.Equal("GotoCatalogEntry", CardClickEffectTestData.CaseName(effects[1]));
        var entryFields = CardClickEffectTestData.Fields(effects[1]);
        Assert.Equal("soak", entryFields[0]!);
        Assert.Null(entryFields[1]);
    }

    [Fact]
    public void Parse_goto_entry_preserving_filters()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module tab t
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
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end tab
            """);

        var effects = doc.Cards[0].ClickBehaviour!.Value.Effects;
        Assert.Equal("SetFilterFromField", CardClickEffectTestData.CaseName(effects[0]));
        Assert.Equal("GotoCatalogEntry", CardClickEffectTestData.CaseName(effects[1]));
        var fields = CardClickEffectTestData.Fields(effects[1]);
        Assert.Equal("detail", fields[0]!);
        var preserved = (Microsoft.FSharp.Core.FSharpOption<IReadOnlyList<string>>)fields[1]!;
        Assert.Equal(["usage_date", "user_name"], preserved.Value);
    }

    [Fact]
    public void Parse_goto_entry_preserving_matching_filters()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            module tab t
              report
              title = "T"
              card nav as "Nav"
              on click
              goto entry detail preserving filters
              end click
              diagram bar
              x = a y = b
              end bar
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end tab
            """);

        var effect = doc.Cards[0].ClickBehaviour!.Value.Effects[0];
        Assert.Equal("GotoCatalogEntry", CardClickEffectTestData.CaseName(effect));
        var fields = CardClickEffectTestData.Fields(effect);
        Assert.Equal("detail", fields[0]!);
        var preserved = (Microsoft.FSharp.Core.FSharpOption<IReadOnlyList<string>>)fields[1]!;
        Assert.NotNull(preserved);
        Assert.Empty(preserved.Value);
    }

    [Fact]
    public void Parse_catalog_group_assigns_group_id()
    {
        var catalog = CatalogParser.Parse("""
            module catalog demo
            
            default soak
            
            group customer
              title = "Customer"
              entry peak as "Peak"
              dashspec "peak.dashspec"
            end group
            
            entry soak as "Soak"
              dashspec "soak.dashspec"
            """);

        Assert.NotNull(catalog.Groups);
        Assert.Single(catalog.Groups.Value);
        Assert.Equal("customer", catalog.Groups.Value[0].Id);
        Assert.Equal("Customer", catalog.Groups.Value[0].Title);
        Assert.Equal("customer", catalog.Entries[0].GroupId.Value);
        Assert.Null(catalog.Entries[1].GroupId);
    }

    [Fact]
    public void Parse_merged_paged_tab_module_allows_non_paged_sibling_tab()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-pages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "overview.ds"), """
                module tab overview
                namespace Paged.Tabs
                report
                title = "Overview"
                card plain as "Plain"
                diagram bar
                x = a y = b
                end bar
                data flow { fixture_src [rows] -> [rows] __diagram__ }
                end card
                end report
                end tab
                """);

            File.WriteAllText(Path.Combine(dir, "paged.ds"), """
                module tab paged_reports
                namespace Paged.Tabs
                report
                title = "Paged reports"
                page p1
                card paged as "Paged"
                diagram bar
                x = a y = b
                end bar
                data flow { fixture_src [rows] -> [rows] __diagram__ }
                end card
                end page
                end report
                end tab
                """);

            var doc = DashSpecTestRowTypes.ParseDashboard("""
                module dashboard soak
                  import types from Fixtures.QueryRowTypes
                  import tabs from Paged.Tabs
                  report
                  title = "Soak"
                  tab overview as "Overview"
                  tab paged_reports as "Paged reports"
                  end report
                end dashboard
                """, dir);

            Assert.Equal(2, doc.Tabs.Count);
            Assert.NotNull(doc.Pages);
            Assert.Single(doc.Pages!.Value);
            Assert.Equal("paged_reports", doc.Pages!.Value[0].TabId);
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
        var board = LayoutModuleParser.ParseLayoutFile("""
            module layout page_grid
            scope page
            [ a b ]
            """);

        Assert.Equal(LayoutScope.Page, board.ModuleScope);
        Assert.Equal(["a", "b"], board.Rows[0]);
    }

    [Fact]
    public void ValidateSpec_rejects_card_outside_page_when_pages_declared()
    {
        Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
            module tab t
              report
              title = "T"
              page overview
              card a as "A"
              diagram bar
              x = a y = b
              end bar
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end page
              card orphan as "Orphan"
              diagram bar
              x = a y = b
              end bar
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end tab
            """));
    }
}
