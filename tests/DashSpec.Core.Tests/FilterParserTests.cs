using DashSpec.Abstractions.Query;
using DashSpec.Execution.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class FilterParserTests
{
    [Fact]
    public void Tokenize_activity_slot_is_single_ident()
    {
        var tokens = DashSpecParser.Tokenize("filter date activity_slot {");
        var idents = tokens.Where(t => t.Kind == TokenKind.Ident).Select(t => t.Value).ToList();
        Assert.Equal(["filter", "date", "activity_slot"], idents);
    }

    [Fact]
    public void Parse_filter_date_block_with_underscore_name()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.activity_slot.range = today
              end defaults
              filter activity_slot
                bind date
                  column = bucket_start_utc
                end bind
                show
                  label = "Day"
              widget = day
                              end show
              end filter
              end report
            end dashboard
""");

        Assert.Equal("activity_slot", doc.Filters.Single().Name);
        Assert.True(doc.Filters.Single().IsDayWidget);
    }

    [Fact]
    public void Parse_on_syntax_filter_followed_by_block_filter()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.usage_date.range = -7d..today
                filter.activity_slot.range = today
              end defaults
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Дата отчёта"
                end show
              end filter
              filter activity_slot
                bind date
                  column = bucket_start_utc
                end bind
                show
                  label = "День"
              widget = day
                              end show
              end filter
              end report
            end dashboard
""");

        Assert.Equal(2, doc.Filters.Count);
        Assert.Equal("activity_slot", doc.Filters.Last().Name);
    }

    [Fact]
    public void Parse_top_filter_defaults_block()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.events_top.limit = 200
              end defaults
              filter events_top
                bind top
                end bind
                show
                  label = "Строк (TOP)"
                end show
              end filter
              end report
            end dashboard
""");

        Assert.Equal("events_top", doc.Filters.Single().Name);
        Assert.Equal("200", doc.Filters.Single().DefaultExpression);
    }

    [Fact]
    public void Parse_period_grain_then_top_filter()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.events_top.limit = 200
              end defaults
              filter period_grain
                bind field
                  column = demo.v_peak_concurrent_by_period.period_grain
                end bind
                show
                  label = "Масштаб: день / месяц / год"
                end show
              end filter
              filter events_top
                bind top
                end bind
                show
                  label = "Строк (TOP)"
                end show
              end filter
              end report
            end dashboard
""");

        Assert.Equal(2, doc.Filters.Count);
    }

    [Fact]
    public void Parse_soak_filters_up_to_period_grain()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.usage_date.range = -7d..today
                filter.activity_slot.range = today
                filter.period_start.range = -7d..today
              end defaults
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Дата отчёта"
                end show
              end filter
              filter activity_slot
                bind date
                  column = bucket_start_utc
                end bind
                show
                  label = "День"
              widget = day
                              end show
              end filter
              filter period_start
                bind date
                  column = period_start
                end bind
                show
                  label = "Начало периода"
                end show
              end filter
              filter app_name
                bind field
                  column = demo.v_daily_active_users.app_name
                end bind
                show
                  label = "Продукты"
                  widget = combobox
                end show
              end filter
              filter user_name
                bind field
                  column = demo.v_events_detail.user_sam
                end bind
                show
                  label = "Пользователь"
                  widget = combobox
                end show
              end filter
              filter period_grain
                bind field
                  column = demo.v_peak_concurrent_by_period.period_grain
                end bind
                show
                  label = "Масштаб: день / месяц / год"
                end show
              end filter
              end report
            end dashboard
""");

        Assert.Equal(6, doc.Filters.Count);
    }

    [Fact]
    public void Parse_soak_filters_section()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.usage_date.range = -7d..today
                filter.activity_slot.range = today
                filter.period_start.range = -7d..today
                filter.events_top.limit = 200
                filter.idle_top.limit = 100
              end defaults
              filter usage_date
                bind date
                  column = usage_date
                end bind
                show
                  label = "Дата отчёта"
                end show
              end filter
              filter activity_slot
                bind date
                  column = bucket_start_utc
                end bind
                show
                  label = "День"
              widget = day
                              end show
              end filter
              filter period_start
                bind date
                  column = period_start
                end bind
                show
                  label = "Начало периода"
                end show
              end filter
              filter app_name
                bind field
                  column = demo.v_daily_active_users.app_name
                end bind
                show
                  label = "Продукты"
                  widget = combobox
                end show
              end filter
              filter user_name
                bind field
                  column = demo.v_events_detail.user_sam
                end bind
                show
                  label = "Пользователь"
                  widget = combobox
                end show
              end filter
              filter period_grain
                bind field
                  column = demo.v_peak_concurrent_by_period.period_grain
                end bind
                show
                  label = "Масштаб: день / месяц / год"
                end show
              end filter
              filter events_top
                bind top
                end bind
                show
                  label = "Строк (TOP)"
                end show
              end filter
              filter idle_top
                bind top
                end bind
                show
                  label = "Строк (TOP)"
                end show
              end filter
              end report
            end dashboard
""");

        Assert.Equal(8, doc.Filters.Count);
    }
    [Fact]
    public void Parse_filter_top_as_on_declaration()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              filter events_top
                bind top
                end bind
                show
                  label = "Строк (TOP)"
                end show
              end filter
              default = 200
              end filter
              end report
            end dashboard
"""));

        Assert.Contains("defaults block", ex.Message);
    }

    [Fact]
    public void Parse_filter_column_as_label()
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
                  label = "Дата отчёта"
                end show
              end filter
              end report
            end dashboard
""");

        var filter = doc.Filters.Single();
        Assert.Equal("usage_date", filter.ColumnReference);
        Assert.Equal("Дата отчёта", filter.Label);
    }

    [Fact]
    public void Parse_filter_default_in_defaults_block_preserves_label()
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
                  label = "Daily"
                              end show
              end filter
              end report
            end dashboard
""");

        var filter = doc.Filters.Single();
        Assert.Equal("-7d..today", filter.DefaultExpression);
        Assert.Equal("Daily", filter.Label);
        Assert.Equal("usage_date", filter.ColumnReference);
    }

    [Fact]
    public void Parse_filter_block_multiline()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.activity_range.range = -1d..today
              end defaults
              filter activity_range
                bind date
                  column = bucket_start_utc
                end bind
                show
                  label = "Activity 5-min"
                end show
              end filter
              end report
            end dashboard
""");

        var filter = doc.Filters.Single();
        Assert.Equal("-1d..today", filter.DefaultExpression);
        Assert.Equal("Activity 5-min", filter.Label);
    }

    [Fact]
    public void Parse_date_filter_inline_widget_day_and_grain_filter()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.period_start.range = today
                filter.activity_slot.range = today
              end defaults
              filter period_start
                bind date
                  column = period_start
                  grain_filter = period_grain
                end bind
                show
                  label = "Период"
                  widget = day
                end show
              end filter
              filter activity_slot
                bind date
                  column = bucket_start_utc
                end bind
                show
                  label = "День"
                  widget = day
                end show
              end filter
              card c as "C"
              diagram table
              columns = a
              end table
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        var period = doc.Filters.Single(f => f.Name == "period_start");
        Assert.Equal("Период", period.Label);
        Assert.Equal("day", period.Widget);
        Assert.Equal("period_grain", period.GrainFilterName);
        Assert.Equal("today..today", period.DefaultExpression);

        var slot = doc.Filters.Single(f => f.Name == "activity_slot");
        Assert.Equal("bucket_start_utc", slot.ColumnReference);
        Assert.Equal("День", slot.Label);
    }

    [Fact]
    public void Parse_date_filter_inline_range_without_widget_does_not_bleed_into_next_line()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.activity_slot.range = today..today
                filter.period_start.range = today
              end defaults
              filter activity_slot
                bind date
                  column = bucket_start_utc
                end bind
                show
                  label = "День"
                end show
              end filter
              filter period_start
                bind date
                  column = period_start
                  grain_filter = period_grain
                end bind
                show
                  label = "Период"
                  widget = day
                end show
              end filter
              card c as "C"
              diagram table
              columns = a
              end table
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        Assert.Equal(2, doc.Filters.Count);
        Assert.Equal("today..today", doc.Filters.Single(f => f.Name == "activity_slot").DefaultExpression);
        Assert.Null(doc.Filters.Single(f => f.Name == "activity_slot").Widget);
    }

    [Fact]
    public void Parse_field_filter_single_select_combobox()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.period_grain.scale = day
              end defaults
              filter period_grain
                bind field
                  column = demo.v_peak.period_grain
                  single = true
                end bind
                show
                  label = "Grain"
                  widget = combobox
                end show
              end filter
              card c as "C"
              diagram table
              columns = a
              end table
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              end card
              end report
            end dashboard
""");

        var grain = doc.Filters.Single(f => f.Name == "period_grain");
        Assert.Equal("combobox", grain.Widget);
        Assert.True(grain.SingleSelect);
        Assert.True(grain.IsSingleSelectField);
        Assert.Equal("day", grain.DefaultExpression);
    }

    [Fact]
    public void Parse_filter_ref_does_not_consume_next_line_filter()
    {
        var doc = DashSpecTestRowTypes.ParseDashboard("""
            @dashboard t
                  !include "query-row-types.dashtype"
              report
              title = "T"
              defaults
                filter.events_top.limit = 200
              end defaults
              filter period_grain
                bind field
                  column = demo.v_peak.period_grain
                end bind
                show
                  label = "Grain"
                end show
              end filter
              filter events_top
                bind top
                end bind
                show
                  label = "Строк (TOP)"
                end show
              end filter
              end report
            end dashboard
""");

        Assert.Equal(2, doc.Filters.Count);
        Assert.Equal("events_top", doc.Filters[1].Name);
    }


    [Fact]
    public void Compile_day_widget_uses_half_open_day_range_not_equality()
    {
        var card = new CardDefinition(
            "activity",
            "Activity",
            new DiagramDefinition("bar", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x"] = "bucket_start_utc",
                ["y"] = "event_count",
            }),
            new DataSourceDefinition(DataSourceKind.View, "demo.v_hourly_activity", RowsType: "FixtureRow"),
            ["activity_slot"],
            []);

        var filters = new FilterState();
        filters.SetDate("activity_slot", new DateOnly(2026, 6, 30), new DateOnly(2026, 6, 30));

        var filterIndex = new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["activity_slot"] = new(
                FilterKind.Date,
                "activity_slot",
                "today..today",
                "bucket_start_utc",
                Widget: "day"),
        };

        var query = QueryCompiler.Compile(card, filters, filterIndex, DashSpecTestRowTypes.Catalog);

        Assert.Contains("bucket_start_utc >= @activity_slot_from", query.Sql);
        Assert.Contains("bucket_start_utc < DATEADD(day, 1, @activity_slot_to)", query.Sql);
        Assert.DoesNotContain("@activity_slot_day", query.Sql, StringComparison.Ordinal);
    }
}
