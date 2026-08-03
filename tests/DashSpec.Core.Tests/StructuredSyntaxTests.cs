using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class StructuredSyntaxTests
{
    [Fact]
    public void Parse_filter_id_first_bind_show()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
              report
                title = "T"
                filters
                  filter usage_date
                    bind date
                      column = usage_date
                      default = -30d..today
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
    }

    [Fact]
    public void Parse_filter_field_qualified_column()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
              report
                title = "T"
                filters
                  filter app_name
                    bind field
                      column = lus.v_peak_concurrent_by_period.app_name
                    end bind
                    show
                      label = "Products"
                    end show
                  end filter
                end filters
              end report
            end dashboard
            """);

        Assert.Equal("lus.v_peak_concurrent_by_period.app_name", doc.Filters.Single().ColumnReference);
    }

    [Fact]
    public void Parse_structured_card_with_override_for()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
              report
                title = "T"
                toolbar usage_date
                filter date usage_date on usage_date as "Дата" default -7d..today
                card peak
                  title = "Peak"
                  data
                    datasource view demo.v_peak
                    bind usage_date
                  end data
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
        Assert.Equal("demo.v_peak", card.DataSource.Value);
        Assert.Single(card.BoundFilters);
        Assert.Equal(1, card.Placement?.Row);
    }
}
