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
            @dashboard t
              report
              title = "T"
              filter date usage_date on usage_date as "Usage" default -7d..today
              filter field app_name on demo.v_daily_active_users.app_name as "App"
              filters dashboard
              usage_date
              app_name
              end dashboard
              card peak as "Peak"
              bind
                usage_date, app_name
              end bind
              diagram line
              x = usage_date
              y = peak_concurrent_proxy
              series = app_name
              end line
              datasource view demo.v_daily_peak_concurrent_proxy
              end card
              end report
            end dashboard
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

            @dashboard t
              configuration
              sqldialect = tsql
              end configuration
              report
              title = "T"
              filter date usage_date
              column = usage_date as "Дата" default
              end filter
              filters dashboard
              usage_date
              end dashboard
              card top as "Top"
              bind
                usage_date
              end bind
              diagram bar
              x = user_sam y
              end bar
              datasource sql query "SELECT user_sam, MAX(n) AS peak_concurrent_apps FROM t GROUP BY user_sam"
              end card
              end report
            end dashboard
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

            @dashboard t
              configuration
              sqldialect = postgres
              end configuration
              report
              title = "T"
              filter date usage_date on usage_date as "Дата" default -7d..today
              filters dashboard
              usage_date
              end dashboard
              card a as "A"
              bind
                usage_date
              end bind
              diagram line
              x = usage_date y
              end line
              datasource view public.metrics
              end card
              end report
            end dashboard
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
            @dashboard t
              report
              title = "T"
              card events as "Events"
              diagram table
              columns = id, name
              limit = 100
              end table
              datasource view dbo.events
              end card
              end report
            end dashboard
""").Cards[0];

        var query = QueryCompiler.Compile(card, new FilterState(), new Dictionary<string, Model.FilterDefinition>());

        Assert.StartsWith("SELECT TOP 100", query.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_table_uses_bound_top_filter()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              filter top row_limit as "Limit"
              default = 250
              end filter
              card events as "Events"
              filters
              row_limit
              end filters
              bind
                row_limit
              end bind
              diagram table
              columns = id, name
              end table
              datasource view dbo.events
              end card
              end report
            end dashboard
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
            @dashboard t
              report
              title = "T"
              filter date usage_date on usage_date as "Дата" default -7d..today
              filter top row_limit as "Limit"
              default = 100
              end filter
              filters dashboard
              usage_date
              end dashboard
              card events as "Events"
              filters
              row_limit
              end filters
              bind
                usage_date, row_limit
              end bind
              diagram table
              columns = id, name
              end table
              datasource view dbo.events
              end card
              end report
            end dashboard
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

    [Fact]
    public void Compile_bar_applies_bound_top_filter_with_order_by()
    {
        var card = new CardDefinition(
            "over",
            "Over",
            new DiagramDefinition("bar", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["category"] = "app_name",
                ["value"] = "peak_concurrent_proxy",
                ["reference"] = "purchased_seats",
                ["order_by"] = "utilization_pct DESC, app_name",
            }),
            new DataSourceDefinition(DataSourceKind.View, "lus.v_stakeholder_peak_over_limit"),
            ["chart_top"],
            []);

        var filters = new FilterState();
        filters.SetTop("chart_top", 15);

        var filterIndex = new Dictionary<string, FilterDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            ["chart_top"] = new(FilterKind.Top, "chart_top", "10", null, MaxValue: 50),
        };

        var query = QueryCompiler.Compile(card, filters, filterIndex);

        Assert.StartsWith("SELECT TOP 15", query.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY utilization_pct DESC, app_name", query.Sql);
        Assert.Contains("SUM(peak_concurrent_proxy) AS peak_concurrent_proxy", query.Sql);
        Assert.Contains("MAX(purchased_seats) AS purchased_seats", query.Sql);
        Assert.Contains("MAX(utilization_pct) AS utilization_pct", query.Sql);
        Assert.Contains("GROUP BY app_name", query.Sql);
    }

    [Fact]
    public void Compile_donut_sums_measure_by_category_after_filters()
    {
        var card = new CardDefinition(
            "by_form",
            "Form",
            new DiagramDefinition("donut", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["category"] = "form",
                ["value"] = "launch_count",
                ["order_by"] = "launch_count DESC, form",
            }),
            new DataSourceDefinition(DataSourceKind.View, "luf.v_launches_by_form"),
            ["usage_date"],
            []);

        var filters = new FilterState();
        filters.SetDate("usage_date", new DateOnly(2026, 7, 7), new DateOnly(2026, 8, 6));
        var index = new Dictionary<string, Model.FilterDefinition>
        {
            ["usage_date"] = new(Model.FilterKind.Date, "usage_date", "-30d..today", "usage_date"),
        };

        var query = QueryCompiler.Compile(card, filters, index, SqlDialect.TSql);

        Assert.Contains("SUM(launch_count) AS launch_count", query.Sql);
        Assert.Contains("GROUP BY form", query.Sql);
        Assert.Contains("usage_date >= @usage_date_from", query.Sql);
        Assert.Contains("ORDER BY launch_count DESC, form", query.Sql);
        Assert.DoesNotContain("SUM(form)", query.Sql);
    }

    [Fact]
    public void Compile_number_defaults_to_sum_over_filtered_rows()
    {
        var card = new CardDefinition(
            "dau_total",
            "DAU",
            new DiagramDefinition("number", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["value"] = "distinct_users",
            }),
            new DataSourceDefinition(DataSourceKind.View, "demo.v_daily_active_users"),
            ["usage_date"],
            []);

        var filters = new FilterState();
        filters.SetDate("usage_date", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7));
        var index = new Dictionary<string, Model.FilterDefinition>
        {
            ["usage_date"] = new(Model.FilterKind.Date, "usage_date", "-7d..today", "usage_date"),
        };

        var query = QueryCompiler.Compile(card, filters, index);

        Assert.Contains("SUM(distinct_users) AS distinct_users", query.Sql);
        Assert.Contains("usage_date >= @usage_date_from", query.Sql);
        Assert.DoesNotContain("GROUP BY", query.Sql);
    }

    [Fact]
    public void Compile_number_max_aggregate_without_group_by()
    {
        var card = new CardDefinition(
            "peak",
            "Peak",
            new DiagramDefinition("number", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["value"] = "peak_concurrent_proxy",
                ["aggregate"] = "max",
            }),
            new DataSourceDefinition(DataSourceKind.View, "demo.v_daily_peak_concurrent_proxy"),
            ["usage_date", "app_name"],
            []);

        var filters = new FilterState();
        filters.SetDate("usage_date", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7));
        filters.SetField("app_name", ["Tekla Structures"]);
        var index = new Dictionary<string, Model.FilterDefinition>
        {
            ["usage_date"] = new(Model.FilterKind.Date, "usage_date", "-7d..today", "usage_date"),
            ["app_name"] = new(Model.FilterKind.Field, "app_name", null, "demo.v_daily_peak_concurrent_proxy.app_name"),
        };

        var query = QueryCompiler.Compile(card, filters, index);

        Assert.Contains("MAX(peak_concurrent_proxy) AS peak_concurrent_proxy", query.Sql);
        Assert.Contains("app_name = @app_name_0", query.Sql);
        Assert.DoesNotContain("GROUP BY", query.Sql);
    }

    [Fact]
    public void Compile_number_aggregate_none_keeps_row_select()
    {
        var card = new CardDefinition(
            "single",
            "Single",
            new DiagramDefinition("number", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["value"] = "kpi",
                ["aggregate"] = "none",
            }),
            new DataSourceDefinition(DataSourceKind.View, "demo.v_kpi"),
            [],
            []);

        var query = QueryCompiler.Compile(card, new FilterState(), new Dictionary<string, Model.FilterDefinition>());

        Assert.Contains("SELECT kpi FROM demo.v_kpi", query.Sql);
        Assert.DoesNotContain("SUM(", query.Sql);
        Assert.DoesNotContain("MAX(", query.Sql);
    }
}
