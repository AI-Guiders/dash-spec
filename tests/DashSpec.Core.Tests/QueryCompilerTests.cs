using DashSpec.Abstractions.Query;
using DashSpec.Execution.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
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
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter date usage_date on usage_date as "Usage"
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
    public void Compile_cell_drill_overlay_overrides_session_filters_for_bound_names()
    {
        var card = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter date usage_date on usage_date as "Usage"
              filter field app_name on demo.v.app_name as "App"
              filters dashboard
              usage_date
              app_name
              end dashboard
              card peak as "Peak"
              bind
                usage_date, app_name
              end bind
              diagram table
              columns = host_name, user_sam
              end table
              datasource view demo.v_drill
              end card
              end report
            end dashboard
""").Cards[0];

        var filters = new FilterState();
        filters.SetDate("usage_date", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7));
        filters.SetField("app_name", ["OtherApp"]);

        var index = new Dictionary<string, Model.FilterDefinition>
        {
            ["usage_date"] = new(Model.FilterKind.Date, "usage_date", "-7d..today", "usage_date"),
            ["app_name"] = new(Model.FilterKind.Field, "app_name", null, "demo.v.app_name"),
        };

        var overlay = new CardCellDrillOverlay();
        overlay.SetDate("usage_date", new DateOnly(2026, 6, 3), new DateOnly(2026, 6, 3));
        overlay.SetField("app_name", "Tekla Structures");

        var query = QueryCompiler.Compile(card, filters, index, cellDrillOverlay: overlay);

        Assert.Contains("usage_date >= @usage_date_from", query.Sql);
        Assert.Contains("app_name = @app_name_0", query.Sql);
        Assert.Equal(new DateOnly(2026, 6, 3), (DateOnly)query.Parameters.First(p => p.Name == "@usage_date_from").Value!);
        Assert.Equal("Tekla Structures", query.Parameters.First(p => p.Name == "@app_name_0").Value);
    }

    [Fact]
    public void Compile_cell_drill_overlay_applies_bucket_start_utc_field()
    {
        var card = DashSpecParser.Parse("""

            @dashboard t
              configuration
              sqldialect = tsql
              end configuration
              report
              title = "T"
              filter field bucket on bucket_start_utc as "Bucket"
              filters dashboard
              bucket
              end dashboard
              card c as "C"
              bind
                bucket
              end bind
              diagram table
              columns = user_sam
              end table
              datasource view lus.v_five_minute_activity_at_bucket
              end card
              end report
            end dashboard
""").Cards[0];

        var filters = new FilterState();
        var index = new Dictionary<string, Model.FilterDefinition>
        {
            ["bucket"] = new(Model.FilterKind.Field, "bucket", null, "bucket_start_utc"),
        };

        var overlay = new CardCellDrillOverlay();
        overlay.SetField("bucket", "2026-06-03T08:05:00");

        var query = QueryCompiler.Compile(card, filters, index, cellDrillOverlay: overlay);

        Assert.Contains("bucket_start_utc = @bucket_0", query.Sql);
        Assert.Equal("2026-06-03T08:05:00", query.Parameters.First(p => p.Name == "@bucket_0").Value);
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
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter date usage_date on usage_date as "Дата"
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
              defaults
                filter.usage_date.range = -7d..today
              end defaults
              filter date usage_date on usage_date as "Дата"
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
    public void Compile_postgres_table_uses_trailing_limit()
    {
        var card = DashSpecParser.Parse("""
            @dashboard t
              configuration
              sqldialect = postgres
              end configuration
              report
              title = "T"
              card events as "Events"
              diagram table
              columns = id, name
              limit = 100
              end table
              datasource view public.events
              end card
              end report
            end dashboard
""").Cards[0];

        var query = QueryCompiler.Compile(card, new FilterState(), new Dictionary<string, Model.FilterDefinition>(), SqlDialect.Postgres);

        Assert.Contains("LIMIT 100", query.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TOP", query.Sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_table_uses_bound_top_filter()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              defaults
                filter.row_limit.limit = 250
              end defaults
              filter top row_limit as "Limit"
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
              defaults
                filter.usage_date.range = -7d..today
                filter.row_limit.limit = 100
              end defaults
              filter date usage_date on usage_date as "Дата"
              filter top row_limit as "Limit"
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

    [Fact]
    public void Compile_working_time_basis_appends_work_window_predicate()
    {
        var card = new CardDefinition(
            "peak",
            "Peak",
            new DiagramDefinition(
                "line",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["x"] = "usage_date",
                    ["y"] = "peak_concurrent_proxy",
                }),
            new DataSourceDefinition(DataSourceKind.View, "demo.v_peak"),
            ["usage_date"],
            []);

        var policy = new ReportTimePolicy(
            ReportTimeBasis.Working,
            ReportTimeApply.Clip,
            "bucket_start_utc",
            new WorkCalendarDefinition("Russian Standard Time", "09:00", "18:00", "mon,tue,wed,thu,fri"));

        var filters = new FilterState();
        filters.SetDate("usage_date", new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 7));
        var index = new Dictionary<string, Model.FilterDefinition>
        {
            ["usage_date"] = new(Model.FilterKind.Date, "usage_date", "-7d..today", "usage_date"),
        };

        var query = QueryCompiler.Compile(card, filters, index, SqlDialect.TSql, reportTimePolicy: policy);

        Assert.Contains("bucket_start_utc AT TIME ZONE 'UTC'", query.Sql);
        Assert.Contains("AT TIME ZONE 'Russian Standard Time'", query.Sql);
        Assert.Contains("CAST(", query.Sql);
        Assert.Contains("09:00:00", query.Sql);
    }

    [Fact]
    public void Parse_configuration_reads_time_basis_and_work_column()
    {
        var document = DashSpecParser.Parse("""
            @dashboard t
              configuration
                time_basis = working
                work_time_column = bucket_start_utc
              end configuration
              report
              title = "T"
              card c as "C"
              diagram number
              value = kpi
              end number
              datasource view demo.v
              end card
              end report
            end dashboard
            """);

        Assert.NotNull(document.TimePolicy);
        Assert.Equal(ReportTimeBasis.Working, document.TimePolicy!.Basis);
        Assert.Equal("bucket_start_utc", document.TimePolicy!.WorkTimeColumn);
    }

    [Fact]
    public void Parse_tab_configuration_accepts_work_time_column()
    {
        var document = DashSpecParser.Parse("""
            @tab overview
              configuration
                sqldialect = tsql
                palette = "palettes/lus-apps.dashpalette"
                work_time_column = bucket_start_utc
              end configuration
              report
              title = "Overview"
              card c as "C"
              diagram number
              value = kpi
              end number
              datasource view demo.v
              end card
              end report
            end tab
            """);

        Assert.Equal("bucket_start_utc", document.TimePolicy?.WorkTimeColumn);
    }
}
