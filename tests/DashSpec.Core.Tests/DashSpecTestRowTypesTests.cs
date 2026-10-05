using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DashSpecTestRowTypesTests
{
    [Fact]
    public void ParseDashboard_appends_rows_to_infer_view_datasource()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
              report
              title = "T"
              card c as "C"
              diagram number
              value = x
              end number
              datasource infer view dbo.events
              end card
              end report
            end dashboard
            """);

        Assert.Equal("FixtureRow", doc.Cards[0].DataSource.RowsType);
    }

    [Fact]
    public void Catalog_lazy_initializes()
    {
        _ = DashSpecTestRowTypes.Catalog;
    }

}
