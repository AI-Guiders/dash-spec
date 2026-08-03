using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class EndSyntaxTests
{
    [Fact]
    public void Parse_end_card_and_page_stakeholder_style()
    {
        var doc = DashSpecParser.Parse("""
            @tab t
              report
              title = "R"
              page peak_util
              card a as "A"
              diagram bar
              category = x value = y
              end bar
              datasource view dbo.t
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
        var doc = DashSpecParser.Parse("""
            @tab t
            
            report
              title = "R"
              card peak_by_app
                title = "Peak"
                diagram bar
                  category = x value = y
                end bar
                datasource view dbo.t
              end card
            end report
            """);

        Assert.Single(doc.Cards);
        Assert.Equal("Peak", doc.Cards[0].Title);
    }

    [Fact]
    public void Parse_page_toolbar_and_derive()
    {
        var doc = DashSpecParser.Parse("""
            @tab t
              report
              title = "R"
              filter date usage_date on usage_date as "Дата" default -7d..today
              filter date period_start on period_start as "Период" default today..today
              standalone
              toolbar usage_date, period_start
              end standalone
              page p
              toolbar usage_date, period_start
              derive usage_date from period_start
              card c as "C"
              diagram bar
              category = x value = y
              end bar
              datasource view dbo.t
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
