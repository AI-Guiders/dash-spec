using DashSpec.Core.Layout;
using DashSpec.Core.Parsing;
using DashSpec.Core.Model;
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
              defaults
                filter.rows_top.limit = 100
                filter.usage_date.range = -7d..today
              end defaults
              filter top rows_top as "Top" ref T
              filter date usage_date on usage_date as "Date"
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
        Assert.Equal(2, placements["D"].Row);
        Assert.Equal(12, placements["D"].Span);
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

    [Fact]
    public void Parse_card_with_two_diagram_slots_and_data_for()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter date usage_date on usage_date as "Date"
              filters dashboard
              usage_date
              end dashboard
              card peak as "Peak"
              diagram ref main heatmap
              x = d
              y = a
              value = v
              end heatmap
              diagram ref drill table
              columns = h, u
              end table
              data
                datasource view dbo.heat
                bind usage_date
              end data
              data for drill
                datasource view dbo.drill
                bind usage_date
              end data
              layout
                [ main ]
                [ drill ]
              end layout
              end card
              end report
            end dashboard
            """);

        var card = doc.Cards.Single();
        Assert.NotNull(card.DiagramSlots);
        Assert.Equal(2, card.DiagramSlots!.Count);
        Assert.NotNull(card.InteriorBoard);
        var placements = CardInteriorLayoutCompactor.Compact(card, doc.Filters, doc.Layout.Columns);
        Assert.Equal(1, placements["main"].Row);
        Assert.Equal(2, placements["drill"].Row);
    }

    [Fact]
    public void Parse_events_detail_diagram_ref_in_interior_layout()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              defaults
                filter.rows_top.limit = 100
                filter.usage_date.range = -7d..today
              end defaults
              filter top rows_top as "Top" ref T
              filter date usage_date on usage_date as "Date"
              filters dashboard
              usage_date
              end dashboard
              card events_detail as "Detail"
              filters rows_top
              data
                datasource view dbo.t
                bind usage_date
              end data
              view
                diagram ref events_table table
                columns = a
                end table
              end view
              layout
                [ rows_top ]
                [ events_table ]
              end layout
              end card
              end report
            end dashboard
            """);

        var card = doc.Cards.Single();
        Assert.Contains("events_table", card.DiagramSlots!.Keys);
        _ = CardInteriorLayoutCompactor.Compact(card, doc.Filters, doc.Layout.Columns);
    }

    [Fact]
    public void Parse_card_filters_apply_splits_local_filter_chrome_order()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter date usage_date on usage_date as "Date"
              filter field app_name on dbo.t.app as "App"
              card peak as "Peak"
              filters
                usage_date
                apply = manual
                app_name
              end filters
              diagram ref H heatmap
              end heatmap
              datasource view dbo.t
              bind usage_date, app_name
              end card
              end report
            end dashboard
            """);

        var card = doc.Cards.Single();
        Assert.True(card.LocalFiltersManualApply);
        Assert.Equal(1, card.LocalFiltersApplySplitIndex);
        Assert.Equal(["usage_date", "app_name"], card.LocalFilters);
    }

    [Fact]
    public void Card_local_filters_layout_board_assigns_weighted_chrome_spans()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              layout grid
              columns = 12
              end grid
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter date usage_date on usage_date as "Date"
              filter field app_name on dbo.t.app as "App" widget combobox
              card peak as "Peak"
              filters
                usage_date
                apply = manual
                app_name
                layout
                  [ usage_date:2 apply:1 app_name:1 ]
                end layout
              end filters
              diagram ref H heatmap
              end heatmap
              datasource view dbo.t
              bind usage_date, app_name
              end card
              end report
            end dashboard
            """);

        var card = doc.Cards.Single();
        var chrome = CardLocalFilterChromeCompactor.Compact(card, doc.Filters, doc.Layout.Columns);
        Assert.Equal(new PlacementDefinition(1, 1, 6), chrome["usage_date"]);
        Assert.Equal(new PlacementDefinition(1, 7, 3), chrome["apply"]);
        Assert.Equal(new PlacementDefinition(1, 10, 3), chrome["app_name"]);
    }

    [Fact]
    public void Synthesized_interior_layout_places_diagram_only_not_head_local_filters()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter date usage_date on usage_date as "Date"
              filter field app_name on dbo.t.app as "App"
              card peak as "Peak"
              filters
                usage_date
                apply = manual
                app_name
              end filters
              diagram ref H heatmap
              end heatmap
              datasource view dbo.t
              bind usage_date, app_name
              end card
              end report
            end dashboard
            """);

        var card = doc.Cards.Single();
        Assert.Null(card.InteriorBoard);

        var placements = CardInteriorLayoutCompactor.Compact(card, doc.Filters, doc.Layout.Columns);
        Assert.Equal(["H"], placements.Keys.OrderBy(static k => k, StringComparer.Ordinal).ToArray());
        Assert.Equal(1, placements["H"].Row);
        Assert.Equal(12, placements["H"].Span);
    }

}
