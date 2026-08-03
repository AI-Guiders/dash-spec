using DashSpec.Core.Layout;
using DashSpec.Core.Parsing;
using Xunit;

namespace DashSpec.Core.Tests;

public class CardInteriorLayoutTests
{
    [Fact]
    public void Parse_card_interior_layout_with_diagram_and_filter_refs()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              filter top rows_top as "Top" ref T default 100
              filter date usage_date on usage_date as "Date" default -7d..today
              filters dashboard
              usage_date
              end dashboard
              card detail as "Detail"
              filters
              rows_top
              end filters
              diagram ref D table
              columns = a, b
              end table
              datasource view dbo.t
              bind
                usage_date
              end bind
              layout
              [ T ]
              [ D ]
              end layout
              end card
              end report
            end dashboard
            """);

        var card = doc.Cards.Single();
        Assert.Equal("D", card.DiagramSlotRef);
        Assert.NotNull(card.InteriorBoard);
        Assert.Equal(2, card.InteriorBoard!.RowCount);

        var placements = CardInteriorLayoutCompactor.Compact(card, doc.Filters, doc.Layout.Columns);
        Assert.Equal(1, placements["rows_top"].Row);
        Assert.Equal(2, placements[CardInteriorSlots.Diagram].Row);
        Assert.Equal(12, placements[CardInteriorSlots.Diagram].Span);
    }

    [Fact]
    public void Parse_rejects_interior_board_missing_diagram_slot()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              filter field app_name on dbo.t.app as "App"
              card detail as "Detail"
              filters
              app_name
              end filters
              diagram number
              value = n
              end number
              datasource view dbo.t
              layout
              [ app_name ]
              end layout
              end card
              end report
            end dashboard
            """));

        Assert.Contains("diagram slot", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_rejects_duplicate_slot_in_interior_board()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              filter field app_name on dbo.t.app as "App" ref A
              card c as "C"
              filters
              app_name
              end filters
              diagram ref D number
              value = x
              end number
              datasource view dbo.t
              layout
              [ A A ]
              end layout
              end card
              end report
            end dashboard
            """));

        Assert.Contains("more than once", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
