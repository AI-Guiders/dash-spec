using DashSpec.Abstractions.Query;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
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
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date activity_slot {
                column = bucket_start_utc as "Day"
                default = today
                widget = day
              }
            }
            """);

        Assert.Equal("activity_slot", doc.Filters.Single().Name);
        Assert.True(doc.Filters.Single().IsDayWidget);
    }

    [Fact]
    public void Parse_on_syntax_filter_followed_by_block_filter()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date on usage_date as "Дата отчёта" default -7d..today
              filter date activity_slot {
                column = bucket_start_utc as "День"
                default = today
                widget = day
              }
            }
            """);

        Assert.Equal(2, doc.Filters.Count);
        Assert.Equal("activity_slot", doc.Filters.Last().Name);
    }

    [Fact]
    public void Parse_top_filter_inline_default()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter top events_top as "Строк (TOP)" default 200
            }
            """);

        Assert.Equal("events_top", doc.Filters.Single().Name);
        Assert.Equal("200", doc.Filters.Single().DefaultExpression);
    }

    [Fact]
    public void Parse_period_grain_then_top_filter()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter field period_grain on demo.v_peak_concurrent_by_period.period_grain as "Масштаб: день / месяц / год"
              filter top events_top as "Строк (TOP)" default 200
            }
            """);

        Assert.Equal(2, doc.Filters.Count);
    }

    [Fact]
    public void Parse_soak_filters_up_to_period_grain()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date on usage_date as "Дата отчёта" default -7d..today
              filter date activity_slot {
                column = bucket_start_utc as "День"
                default = today
                widget = day
              }
              filter date period_start on period_start as "Начало периода" default -7d..today
              filter field app_name on demo.v_daily_active_users.app_name as "Продукты" widget combobox
              filter field user_name on demo.v_events_detail.user_sam as "Пользователь" widget combobox
              filter field period_grain on demo.v_peak_concurrent_by_period.period_grain as "Масштаб: день / месяц / год"
            }
            """);

        Assert.Equal(6, doc.Filters.Count);
    }

    [Fact]
    public void Parse_soak_filters_section()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date on usage_date as "Дата отчёта" default -7d..today
              filter date activity_slot {
                column = bucket_start_utc as "День"
                default = today
                widget = day
              }
              filter date period_start on period_start as "Начало периода" default -7d..today
              filter field app_name on demo.v_daily_active_users.app_name as "Продукты" widget combobox
              filter field user_name on demo.v_events_detail.user_sam as "Пользователь" widget combobox
              filter field period_grain on demo.v_peak_concurrent_by_period.period_grain as "Масштаб: день / месяц / год"
              filter top events_top as "Строк (TOP)" default 200
              filter top idle_top as "Строк (TOP)" default 100
            }
            """);

        Assert.Equal(8, doc.Filters.Count);
    }
    [Fact]
    public void Parse_filter_top_as_on_declaration()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter top events_top as "Строк (TOP)" {
                default = 200
              }
            }
            """);

        var filter = doc.Filters.Single();
        Assert.Equal("Строк (TOP)", filter.Label);
        Assert.Equal("200", filter.DefaultExpression);
    }

    [Fact]
    public void Parse_filter_column_as_label()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date {
                column = usage_date as "Дата отчёта"
                default = -7d..today
              }
            }
            """);

        var filter = doc.Filters.Single();
        Assert.Equal("usage_date", filter.ColumnReference);
        Assert.Equal("Дата отчёта", filter.Label);
    }

    [Fact]
    public void Parse_filter_default_does_not_swallow_label_on_same_line()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date usage_date {
                column = usage_date as "Daily" default = -7d..today
              }
            }
            """);

        var filter = doc.Filters.Single();
        Assert.Equal("-7d..today", filter.DefaultExpression);
        Assert.Equal("Daily", filter.Label);
        Assert.Equal("usage_date", filter.ColumnReference);
    }

    [Fact]
    public void Parse_filter_block_multiline()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date activity_range {
                column = bucket_start_utc as "Activity 5-min"
                default = -1d..today
              }
            }
            """);

        var filter = doc.Filters.Single();
        Assert.Equal("-1d..today", filter.DefaultExpression);
        Assert.Equal("Activity 5-min", filter.Label);
    }

    [Fact]
    public void Parse_date_filter_inline_widget_day_and_grain_filter()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date period_start on period_start as "Период" default today widget day grain_filter period_grain
              filter date activity_slot on bucket_start_utc as "День" default today widget day
              card c as "C" {
                diagram table { columns = a }
                datasource view dbo.t
              }
            }
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
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter date activity_slot on bucket_start_utc as "День" default today..today
              filter date period_start on period_start as "Период" default today widget day grain_filter period_grain
              card c as "C" {
                diagram table { columns = a }
                datasource view dbo.t
              }
            }
            """);

        Assert.Equal(2, doc.Filters.Count);
        Assert.Equal("today..today", doc.Filters.Single(f => f.Name == "activity_slot").DefaultExpression);
        Assert.Null(doc.Filters.Single(f => f.Name == "activity_slot").Widget);
    }

    [Fact]
    public void Parse_field_filter_single_select_combobox()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter field period_grain on demo.v_peak.period_grain as "Grain" default day widget combobox single
              card c as "C" {
                diagram table { columns = a }
                datasource view dbo.t
              }
            }
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
        var doc = DashSpecParser.Parse("""
            @dashboard t
            dashboard "T" {
              filter field period_grain on demo.v_peak.period_grain as "Grain"
              filter top events_top as "Строк (TOP)" default 200
            }
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
            new DataSourceDefinition(DataSourceKind.View, "lus.v_hourly_activity"),
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

        var query = QueryCompiler.Compile(card, filters, filterIndex, SqlDialect.TSql);

        Assert.Contains("bucket_start_utc >= @activity_slot_from", query.Sql);
        Assert.Contains("bucket_start_utc < DATEADD(day, 1, @activity_slot_to)", query.Sql);
        Assert.DoesNotContain("@activity_slot_day", query.Sql, StringComparison.Ordinal);
    }
}
