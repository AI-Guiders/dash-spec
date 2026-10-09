using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class EndSyntaxTests
{
    [Fact]
    public void Parse_end_card_and_page_scoped_layout()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @tab t
              report
              title = "R"
              page peak_util
              card a as "A"
              diagram bar
              category = x value = y
              end bar
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end page
              end report
            end tab
            """);

        Assert.Single(doc.Cards);
        Assert.Equal("peak_util", doc.Cards[0].PageId);
    }

    [Fact]
    public void Parse_end_syntax_card_with_title()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @tab t
            
            report
              title = "R"
              card peak_by_app
                title = "Peak"
                diagram bar
                  category = x value = y
                end bar
                data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
            end report
            """);

        Assert.Single(doc.Cards);
        Assert.Equal("Peak", doc.Cards[0].Title);
    }

    [Fact]
    public void Parse_page_toolbar_and_derive()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @tab t
              report
              title = "R"
              standalone
              defaults
                filter.usage_date.range = -7d..today
                filter.period_start.range = today..today
              end defaults
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Дата"
                end show
              end filter
              filter period_start
                bind date
                  column = period_start
                end bind
                show
                  label = "Период"
                end show
              end filter
              toolbar usage_date, period_start
              end standalone
              page p
              show flow
              usage_date -> [toolbar] chrome.page.p
              period_start -> [toolbar] chrome.page.p
              end show flow
              derive usage_date from period_start
              card c as "C"
              diagram bar
              category = x value = y
              end bar
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              bind
                usage_date
              end bind
              end card
              end page
              end report
            end tab
            """);

        var page = doc.Pages!.Single(p => p.Id == "p");
        Assert.NotNull(page.ToolbarBoard);
        Assert.NotNull(page.UsageDateDerive);
    }
}
