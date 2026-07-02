using DashSpec.Abstractions.Query;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class TabModuleTests
{
    [Fact]
    public void Parse_tab_dashspec_ignores_module_shell_filters_when_embedded()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-tab-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "extra.dashspec"), """
                @tab extra

                filter field app_name on dbo.apps.name as "Apps"

                tab extra as "Extra" {
                  filter top n as "Top" default 5
                }

                card x as "X" {
                  diagram number { value = n }
                  datasource view dbo.x
                }
                """);

            var doc = DashSpecParser.Parse("""
                @dashboard t
                dashboard "T" {
                  filter field app_name on dbo.apps.name as "Apps"
                  tab overview as "Overview" {
                    cards { a }
                  }
                  tab extra dashspec "extra.dashspec"
                  card a as "A" {
                    diagram number { value = n }
                    datasource view dbo.a
                  }
                }
                """, dir);

            Assert.Single(doc.Filters, f => f.Name == "app_name");
            Assert.Single(doc.Filters, f => f.Name == "n");
            Assert.Equal(2, doc.Cards.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parse_tab_dashspec_merges_module_cards()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-tab-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "extra.dashspec"), """
                @tab extra

                card x as "X" {
                  diagram number { value = n }
                  datasource view dbo.x
                }
                """);

            var doc = DashSpecParser.Parse("""
                @dashboard t
                dashboard "T" {
                  tab overview as "Overview" {
                    cards { a }
                  }
                  tab extra dashspec "extra.dashspec"
                  card a as "A" {
                    diagram number { value = n }
                    datasource view dbo.a
                  }
                }
                """, dir);

            Assert.Equal(2, doc.Cards.Count);
            Assert.Equal("extra", doc.Tabs[1].Id);
            Assert.Equal(["x"], doc.Tabs[1].CardIds);
            Assert.Equal("extra", doc.Cards.Single(c => c.Id == "x").TabId);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Parse_tab_dashspec_requires_spec_directory()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              tab x dashspec "x.dashspec"
              card a as "A" {
                diagram number { value = n }
                datasource view dbo.a
              }
            }
            """));

        Assert.Contains("specDirectory", ex.Message);
    }

    [Fact]
    public void Parse_tab_root_standalone_document()
    {
        var doc = DashSpecParser.Parse("""
            @tab soak

            connector sqlserver
            filter date usage_date on usage_date as "Date" default -7d..today
            toolbar { usage_date }

            tab soak as "Soak title"

            card a as "A" {
              bind usage_date
              diagram number { value = n }
              datasource view dbo.a
            }
            """);

        Assert.Equal("soak", doc.Id);
        Assert.Equal("Soak title", doc.Title);
        Assert.Single(doc.Tabs);
        Assert.Equal("sqlserver", doc.ConnectorId);
        Assert.Single(doc.Cards);
    }

    [Fact]
    public void ReadDashboardHeader_reads_tab_root_id()
    {
        const string text = """
            @runtime "cfg.toml"
            @tab stakeholder
            connector sqlserver
            card a as "A" {
              diagram number { value = x }
              datasource view dbo.t
            }
            """;

        Assert.Equal(("stakeholder", "stakeholder"), DashSpecParser.ReadDashboardHeader(text));
    }
}
