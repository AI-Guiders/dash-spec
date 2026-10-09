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
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.rows_top.limit = 100
                filter.usage_date.range = -7d..today
              end defaults
              filter rows_top
                bind top
                end bind
                show
                  label = "Top"
                  ref = T
                end show
              end filter
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Date"
                end show
              end filter
              filters dashboard
              usage_date
              end dashboard
              card detail as "Detail"
              show flow
              rows_top -> [toolbar] chrome.card.detail
              end show flow
              diagram ref D table
              columns = a, b
              end table
              data flow { fixture_src [rows] -> [rows] D }
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
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              filter app_name
                bind field
                  column = dbo.t.app
                end bind
                show
                  label = "App"
                end show
              end filter
              card detail as "Detail"
              show flow
              app_name -> [toolbar] chrome.card.detail
              end show flow
              diagram number
              value = n
              end number
              data flow { fixture_src [rows] -> [rows] __diagram__ }
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
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              filter app_name
                bind field
                  column = dbo.t.app
                end bind
                show
                  label = "App"
                  ref = A
                end show
              end filter
              card c as "C"
              show flow
              app_name -> [toolbar] chrome.card.c
              end show flow
              diagram ref D number
              value = x
              end number
              data flow { fixture_src [rows] -> [rows] D }
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
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Date"
                end show
              end filter
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
              data flow
              fixture_src [rows] -> [rows] main
              usage_date -> [usage_date] main
              fixture_src2 [rows] -> [rows] drill
              usage_date -> [usage_date] drill
              end data flow
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
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.rows_top.limit = 100
                filter.usage_date.range = -7d..today
              end defaults
              filter rows_top
                bind top
                end bind
                show
                  label = "Top"
                  ref = T
                end show
              end filter
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Date"
                end show
              end filter
              filters dashboard
              usage_date
              end dashboard
              card events_detail as "Detail"
              show flow
              rows_top -> [toolbar] chrome.card.events_detail
              end show flow
              data flow
              fixture_src [rows] -> [rows] events_table
              usage_date -> [usage_date] events_table
              end data flow
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
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Date"
                end show
              end filter
              filter app_name
                bind field
                  column = dbo.t.app
                end bind
                show
                  label = "App"
                end show
              end filter
              card peak as "Peak"
              show flow
                usage_date -> [toolbar] chrome.card.peak
                app_name -> [toolbar] chrome.card.peak
              end show flow
              filters
                apply = manual
              end filters
              diagram ref H heatmap
              end heatmap
              data flow { fixture_src [rows] -> [rows] H }
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
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              layout grid
              columns = 12
              end grid
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Date"
                end show
              end filter
              filter app_name
                bind field
                  column = dbo.t.app
                end bind
                show
                  label = "App"
                  widget = combobox
                end show
              end filter
              card peak as "Peak"
              show flow
                usage_date -> [toolbar] chrome.card.peak
                app_name -> [toolbar] chrome.card.peak
              end show flow
              filters
                apply = manual
                layout
                  [ usage_date:2 apply:1 app_name:1 ]
                end layout
              end filters
              diagram ref H heatmap
              end heatmap
              data flow { fixture_src [rows] -> [rows] H }
              bind usage_date, app_name
              end card
              end report
            end dashboard
            """);

        var card = doc.Cards.Single();
        var chrome = CardLocalFilterChromeCompactor.Compact(card, doc.Filters, doc.Layout.Columns);
        Assert.Equal(new PlacementDefinition(1, 1, 2), chrome["usage_date"]);
        Assert.Equal(new PlacementDefinition(1, 3, 1), chrome["apply"]);
        Assert.Equal(new PlacementDefinition(1, 4, 1), chrome["app_name"]);
    }

    [Fact]
    public void Synthesized_interior_layout_places_diagram_only_not_head_local_filters()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Date"
                end show
              end filter
              filter app_name
                bind field
                  column = dbo.t.app
                end bind
                show
                  label = "App"
                end show
              end filter
              card peak as "Peak"
              show flow
                usage_date -> [toolbar] chrome.card.peak
                app_name -> [toolbar] chrome.card.peak
              end show flow
              filters
                apply = manual
              end filters
              diagram ref H heatmap
              end heatmap
              data flow { fixture_src [rows] -> [rows] H }
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
