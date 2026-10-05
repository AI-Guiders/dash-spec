using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class CardFoldChromeTests
{
    [Fact]
    public void Parse_card_chrome_fold_independent()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @tab t
              report
              title = "T"
              card c as "C"
              chrome
                fold = independent
              end chrome
              diagram heatmap
              x = a y
              value = c
              end heatmap
              datasource infer view dbo.t rows FixtureRow
              end card
              end report
            end tab
            """);

        Assert.Equal(CardFoldMode.Independent, doc.Cards[0].Chrome?.Fold);
    }

}
