using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class StructuredSyntaxTests
{
    [Fact]
    public void Parse_filter_id_first_bind_show()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  import types from Fixtures.QueryRowTypes
              report
                title = "T"
                filters
                  defaults
                    filter.usage_date.range = -30d..today
                  end defaults
                  filter usage_date
                    bind date
                      column = usage_date
                    end bind
                    show
                      label = "Дата отчёта"
                    end show
                  end filter
                end filters
              end report
            end dashboard
            """);

        var filter = doc.Filters.Single();
        Assert.Equal("usage_date", filter.Name);
        Assert.Equal(FilterKind.Date, filter.Kind);
        Assert.Equal("usage_date", filter.ColumnReference);
        Assert.Equal("Дата отчёта", filter.Label);
        Assert.Equal("-30d..today", filter.DefaultExpression);
    }

    [Fact]
    public void Parse_filter_field_qualified_column()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  import types from Fixtures.QueryRowTypes
              report
                title = "T"
                filters
                  filter app_name
                    bind field
                      column = demo.v_peak_concurrent_by_period.app_name
                    end bind
                    show
                      label = "Products"
                    end show
                  end filter
                end filters
              end report
            end dashboard
            """);

        Assert.Equal("demo.v_peak_concurrent_by_period.app_name", doc.Filters.Single().ColumnReference);
    }

    [Fact]
    public void Parse_structured_card_with_override_for()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  import types from Fixtures.QueryRowTypes
              report
                title = "T"
                defaults
                  filter.usage_date.range = -7d..today
                end defaults
                toolbar usage_date
                filter usage_date
                  bind date
                    column = usage_date
                  end bind
                  show
                    label = "Дата"
                  end show
                end filter
                card peak
                  title = "Peak"
                  data flow { fixture_src [rows] -> [rows] demo_peak_bar }
                  bind usage_date
                  view
                    diagram demo_peak_bar
                  end view
                  override for demo_peak_bar
                    series max = 12
                  end override
                  layout
                    place
                      row = 1
                      col = 1
                      span = 6
                    end place
                  end layout
                end card
              end report
            end dashboard
            """);

        var card = doc.Cards.Single();
        Assert.Equal("demo_peak_bar", card.Diagram.UsePreset);
        Assert.Equal(12, card.SeriesTransform?.Max);
        Assert.Equal("dbo.t", card.DataSource.Value);
        Assert.Single(card.BoundFilters);
        Assert.Equal(1, card.Placement?.Row);
    }
}
