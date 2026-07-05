using DashSpec.Abstractions.Query;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class QueryCompilerTests
{
    [Fact]
    public void Compile_applies_optional_date_and_field_filters()
    {
        var card = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              filter date usage_date {
                column = usage_date as "Usage"
                default = -7d..today
              }
              filter field app_name { column = demo.v_daily_active_users.app_name as "App" }
              filters dashboard { usage_date, app_name }
              card peak as "Peak" {
                bind usage_date, app_name
                diagram line {
                  x = usage_date
                  y = peak_concurrent_proxy
                  series = app_name
                }
                datasource view demo.v_daily_peak_concurrent_proxy
              }
            }
            }
""").Cards[0];

        var filters = new FilterState();
        filters.SetDate("usage_date", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7));
        filters.SetField("app_name", ["Tekla Structures"]);

        var index = new Dictionary<string, Model.FilterDefinition>
        {
            ["usage_date"] = new(Model.FilterKind.Date, "usage_date", "-7d..today", "usage_date"),
            ["app_name"] = new(Model.FilterKind.Field, "app_name", null, "demo.v_daily_active_users.app_name"),
        };

        var query = QueryCompiler.Compile(card, filters, index);

        Assert.Contains("usage_date >= @usage_date_from", query.Sql);
        Assert.Contains("app_name = @app_name_0", query.Sql);
        Assert.Equal(3, query.Parameters.Count);
    }

    [Fact]
    public void Compile_sql_datasource_wraps_subquery_and_applies_filters()
    {
        var card = DashSpecParser.Parse("""

            @dashboard t {
              configuration { sqldialect = tsql }
              report "T" {
              filter date usage_date { column = usage_date as "Дата" default = -7d..today }
              filters dashboard { usage_date }
              card top as "Top" {
                bind usage_date
                diagram bar { x = user_sam y = peak_concurrent_apps }
                datasource sql query "SELECT user_sam, MAX(n) AS peak_concurrent_apps FROM t GROUP BY user_sam"
              }
            }
            }
""").Cards[0];

        var filters = new FilterState();
        filters.SetDate("usage_date", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7));
        var index = new Dictionary<string, Model.FilterDefinition>
        {
            ["usage_date"] = new(Model.FilterKind.Date, "usage_date", "-7d..today", "usage_date"),
        };

        var query = QueryCompiler.Compile(card, filters, index, SqlDialect.TSql);

        Assert.Contains("FROM (SELECT user_sam", query.Sql);
        Assert.Contains(") AS _dashspec_q", query.Sql);
        Assert.Contains("DATEADD(day, 1, @usage_date_to)", query.Sql);
    }

    [Fact]
    public void Compile_postgres_dialect_uses_interval_for_date_upper_bound()
    {
        var card = DashSpecParser.Parse("""

            @dashboard t {
              configuration { sqldialect = postgres }
              report "T" {
              filter date usage_date {
                column = usage_date as "Дата"
                default = -7d..today
              }
              filters dashboard { usage_date }
              card a as "A" {
                bind usage_date
                diagram line { x = usage_date y = n }
                datasource view public.metrics
              }
            }
            }
""").Cards[0];

        var filters = new FilterState();
        filters.SetDate("usage_date", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7));
        var index = new Dictionary<string, Model.FilterDefinition>
        {
            ["usage_date"] = new(Model.FilterKind.Date, "usage_date", null, "usage_date"),
        };

        var query = QueryCompiler.Compile(card, filters, index, SqlDialect.Postgres);

        Assert.Contains("INTERVAL '1 day'", query.Sql);
        Assert.DoesNotContain("DATEADD", query.Sql);
    }

    [Fact]
    public void Compile_table_uses_top_limit()
    {
        var card = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              card events as "Events" {
                diagram table {
                  columns = id, name
                  limit = 100
                }
                datasource view dbo.events
              }
            }
            }
""").Cards[0];

        var query = QueryCompiler.Compile(card, new FilterState(), new Dictionary<string, Model.FilterDefinition>());

        Assert.StartsWith("SELECT TOP 100", query.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_table_uses_bound_top_filter()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              filter top row_limit as "Limit" {
                default = 250
              }
              card events as "Events" {
                filters { row_limit }
                bind row_limit
                diagram table {
                  columns = id, name
                }
                datasource view dbo.events
              }
            }
            }
""");

        var card = doc.Cards[0];
        var index = DashboardBootstrap.IndexFilters(doc);
        var filters = new FilterState();
        filters.SetTop("row_limit", 75);

        var query = QueryCompiler.Compile(card, filters, index);

        Assert.StartsWith("SELECT TOP 75", query.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_period_start_with_grain_filter_uses_period_anchor()
    {
        var card = new CardDefinition(
            "peak",
            "Peak",
            new DiagramDefinition("bar", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["x"] = "app_name",
                ["y"] = "peak_concurrent_proxy",
            }),
            new DataSourceDefinition(DataSourceKind.View, "lus.v_peak_concurrent_by_period"),
            ["period_grain", "period_start", "app_name"],
            []);

        var filters = new FilterState();
        filters.SetField("period_grain", ["month"]);
        filters.SetDate("period_start", new DateOnly(2026, 6, 24), new DateOnly(2026, 6, 24));

        var filterIndex = new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["period_grain"] = new(FilterKind.Field, "period_grain", "day", "lus.v_peak.period_grain"),
            ["period_start"] = new(
                FilterKind.Date,
                "period_start",
                "today..today",
                "period_start",
                GrainFilterName: "period_grain"),
            ["app_name"] = new(FilterKind.Field, "app_name", null, "lus.v_peak.app_name"),
        };

        var query = QueryCompiler.Compile(card, filters, filterIndex);

        Assert.Contains("period_start = @period_start_anchor", query.Sql);
        Assert.Contains("period_grain = @period_grain_0", query.Sql);
        Assert.Equal(new DateOnly(2026, 6, 1), query.Parameters.Single(p => p.Name == "@period_start_anchor").Value);
    }


    [Fact]
    public void Compile_bound_top_filter_does_not_add_where_clause()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t {
              report "T" {
              filter date usage_date {
                column = usage_date as "Дата"
                default = -7d..today
              }
              filter top row_limit as "Limit" { default = 100 }
              filters dashboard { usage_date }
              card events as "Events" {
                filters { row_limit }
                bind usage_date, row_limit
                diagram table { columns = id, name }
                datasource view dbo.events
              }
            }
            }
""");

        var card = doc.Cards[0];
        var index = DashboardBootstrap.IndexFilters(doc);
        var filters = new FilterState();
        filters.SetDate("usage_date", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7));
        filters.SetTop("row_limit", 50);

        var query = QueryCompiler.Compile(card, filters, index);

        Assert.Contains("usage_date >= @usage_date_from", query.Sql);
        Assert.DoesNotContain("row_limit", query.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("SELECT TOP 50", query.Sql, StringComparison.OrdinalIgnoreCase);
    }
}
