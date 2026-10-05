using Xunit;

namespace DashSpec.Core.Tests;

public sealed class DashSpecTestRowTypesTests
{
    [Fact]
    public void ParseDashboard_requires_explicit_rows_on_datasource()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              card c as "C"
              diagram number
              value = x
              end number
              datasource infer view dbo.events rows FixtureRow
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
