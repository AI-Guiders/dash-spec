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
                @tab extra {
                  report {
                    filters {
                      filter top n as "Top" default 5
                    }
                    card x as "X" {
                      diagram number { value = n }
                      datasource view dbo.x
                    }
                  }
                }
                """);

            var doc = DashSpecParser.Parse("""
                @dashboard t {
                  report "T" {
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
    public void Validate_allows_binding_filter_hosted_on_another_card()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
                filter date period_start on p as "Period" default today widget day
                card host as "Host" {
                  filters { period_start }
                  bind period_start
                  diagram number { value = n }
                  datasource view dbo.t
                }
                card guest as "Guest" {
                  filters host host { period_start }
                  bind period_start
                  diagram number { value = n }
                  datasource view dbo.t
                }
              }
            }
            """);

        Assert.Equal("host", doc.Cards.Single(c => c.Id == "guest").FilterHostCardId);
        Assert.Equal(["period_start"], doc.Cards.Single(c => c.Id == "guest").HostedFilters);
    }

    [Fact]
    public void Validate_rejects_binding_filter_without_explicit_host()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
                filter date period_start on p as "Period" default today widget day
                card host as "Host" {
                  filters { period_start }
                  bind period_start
                  diagram number { value = n }
                  datasource view dbo.t
                }
                card guest as "Guest" {
                  bind period_start
                  diagram number { value = n }
                  datasource view dbo.t
                }
              }
            }
            """));

        Assert.Contains("filters host", ex.Message);
    }

    [Fact]
    public void Parse_tab_dashspec_merges_module_cards()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-tab-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "extra.dashspec"), """
                @tab extra {
                  report {
                    card x as "X" {
                      diagram number { value = n }
                      datasource view dbo.x
                    }
                  }
                }
                """);

            var doc = DashSpecParser.Parse("""
                @dashboard t {
                  report "T" {
                    tab overview as "Overview" {
                      cards { a }
                    }
                    tab extra dashspec "extra.dashspec"
                    card a as "A" {
                      diagram number { value = n }
                      datasource view dbo.a
                    }
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
            @dashboard t {
              report "T" {
                tab x dashspec "x.dashspec"
                card a as "A" {
                  diagram number { value = n }
                  datasource view dbo.a
                }
              }
            }
            """));

        Assert.Contains("specDirectory", ex.Message);
    }

    [Fact]
    public void Parse_tab_root_standalone_document()
    {
        var doc = DashSpecParser.Parse("""
            @tab soak {
              wiring { use connector sqlserver }
              report "Soak title" {
                standalone {
                  filter date usage_date on usage_date as "Date" default -7d..today
                  toolbar { usage_date }
                }
                card a as "A" {
                  bind usage_date
                  diagram number { value = n }
                  datasource view dbo.a
                }
              }
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
            @tab stakeholder {
              runtime { manifest = "cfg.toml" }
              wiring { use connector sqlserver }
              report {
                card a as "A" {
                  diagram number { value = x }
                  datasource view dbo.t
                }
              }
            }
            """;

        Assert.Equal(("stakeholder", "stakeholder"), DashSpecParser.ReadDashboardHeader(text));
    }
}
