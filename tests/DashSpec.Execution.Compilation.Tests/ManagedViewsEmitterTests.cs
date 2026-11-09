using DashSpec.Execution.Compilation.Views;
using DashSpec.Modeling.Core;
using Xunit;

namespace DashSpec.Execution.Compilation.Tests;

/// <summary>ADR-0125: `module flow &lt;id&gt; view` chains emit as content-addressed
/// managed views; unmarked flows never touch the database.</summary>
public sealed class ManagedViewsEmitterTests
{
    private const string TypesText = """
        module types demo_types

        type EventRow
          date usage_date
          string app_name
          int sessions
        end type
        """;

    private static TypeCatalog Catalog() =>
        TypeCatalogModule.ofDefinitions(
            DashSpec.Modeling.Parse.Types.TypeModuleParser.parseTypesModule(TypesText));

    private static DashSpec.Modeling.Parse.DataFlow.DashflowModule ParseFlow(string flowText) =>
        DashSpec.Modeling.Parse.DataFlow.DashflowModuleParser.parseModule(flowText, Catalog());

    private const string ViewFlow = """
        module flow usage_report view

        source events {
          use provider infer
          from view dbo.events
          ports
            output stream rows: EventRow
          end ports
        }

        transformer by_app {
          transform use summarize {
            by = app_name
            agg = sum
            value = sessions
          }
        }

        events [rows] -> [raw] by_app

        end flow
        """;

    [Fact]
    public void Marked_flow_emits_one_view_per_terminal_chain()
    {
        var views = ManagedViewsEmitter.Emit(ParseFlow(ViewFlow), Catalog());
        var view = Assert.Single(views);

        Assert.StartsWith("dashspec_usage_report_by_app_", view.ViewName);
        Assert.Equal(12, view.ViewName[^12..].Length);
        Assert.Contains("GROUP BY app_name", view.Select);
        Assert.Contains("SUM(sessions) AS value", view.Select);
        Assert.StartsWith("CREATE OR ALTER VIEW", view.Ddl);
    }

    [Fact]
    public void Unmarked_flow_emits_nothing()
    {
        var unmarked = ViewFlow.Replace("module flow usage_report view", "module flow usage_report");
        Assert.Empty(ManagedViewsEmitter.Emit(ParseFlow(unmarked), Catalog()));
    }

    [Fact]
    public void Content_addressing_same_select_same_name_changed_content_new_name()
    {
        var first = Assert.Single(ManagedViewsEmitter.Emit(ParseFlow(ViewFlow), Catalog()));

        // identical flow text → identical physical name (safe shared artifact)
        var again = Assert.Single(ManagedViewsEmitter.Emit(ParseFlow(ViewFlow), Catalog()));
        Assert.Equal(first.ViewName, again.ViewName);

        // different logic under the same human names → different sha, no collision
        var changed = ViewFlow.Replace("by = app_name", "by = usage_date");
        var second = Assert.Single(ManagedViewsEmitter.Emit(ParseFlow(changed), Catalog()));
        Assert.NotEqual(first.ViewName, second.ViewName);
        Assert.StartsWith("dashspec_usage_report_by_app_", second.ViewName);
    }

    [Fact]
    public void Order_step_is_not_a_view_body_citizen()
    {
        // A VIEW cannot carry ORDER BY (tsql rejects it without TOP); the step rides
        // the reader's query instead (ADR-0126 query-order vs display-sort).
        var ordered = ViewFlow.Replace(
            "by = app_name\n    agg = sum\n    value = sessions",
            "by = app_name\n    agg = sum\n    value = sessions\n  }\n  transform use order {\n    by = value\n    direction = desc");

        var view = Assert.Single(ManagedViewsEmitter.Emit(ParseFlow(ordered), Catalog()));
        Assert.DoesNotContain("ORDER BY", view.Select);
    }

    [Fact]
    public void Runtime_transforms_never_enter_the_view()
    {
        var withRuntime = ViewFlow.Replace(
            "events [rows] -> [raw] by_app",
            """
            transformer localized {
              transform use to_zone { zone = UTC+3 }
            }

            by_app [rows] -> [raw] localized

            events [rows] -> [raw] by_app
            """);

        var views = ManagedViewsEmitter.Emit(ParseFlow(withRuntime), Catalog());
        var view = Assert.Single(views);
        Assert.DoesNotContain("UTC", view.Select);
    }

    [Fact]
    public void Postgres_dialect_uses_create_or_replace()
    {
        var view = Assert.Single(ManagedViewsEmitter.Emit(ParseFlow(ViewFlow), Catalog(), "postgres"));
        Assert.StartsWith("CREATE OR REPLACE VIEW", view.Ddl);
    }

    private static DashSpec.Modeling.Parse.DataFlow.DashflowModule WithFilterBefore(
        DashSpec.Modeling.Parse.DataFlow.DashflowModule module,
        string paramType,
        string column) =>
        module.WithTransformers([.. module.Transformers, FilterNode(paramType, column)])
            .WithLinks(
            [
                new DashSpec.Modeling.Parse.DataFlow.FlowLinkDef(
                    "events",
                    "rows",
                    "filter.product",
                    "raw"),
                new DashSpec.Modeling.Parse.DataFlow.FlowLinkDef(
                    "filter.product",
                    "rows",
                    "by_app",
                    "raw"),
            ]);

    private static DashSpec.Modeling.Parse.DataFlow.DashflowTransformerDef FilterNode(string paramType, string column) =>
        new(
            "filter.product",
            [],
            [System.Tuple.Create("rows", "")],
            [new DashSpec.Modeling.Parse.DataFlow.DashflowParamPortDef(
                "product",
                paramType,
                null,
                null,
                Microsoft.FSharp.Core.FSharpOption<string>.Some(column))],
            Microsoft.FSharp.Core.FSharpOption<string>.Some("rows"),
            Microsoft.FSharp.Core.FSharpOption<string>.Some("rows"),
            []);

    [Fact]
    public void Bound_filter_param_turns_the_chain_into_an_itvf()
    {
        var withFilter = WithFilterBefore(ParseFlow(ViewFlow), "string", "app_name");
        var view = Assert.Single(ManagedViewsEmitter.Emit(withFilter, Catalog()));
        Assert.True(view.IsFunction);
        Assert.StartsWith("CREATE OR ALTER FUNCTION", view.Ddl);
        Assert.Contains("@product NVARCHAR(400)", view.Ddl);
        Assert.Contains("RETURNS TABLE", view.Ddl);
        Assert.Contains("WHERE app_name = @product", view.Select);
        // the filter rides the innermost select — BEFORE the GROUP BY
        Assert.True(view.Select.IndexOf("WHERE", StringComparison.Ordinal) < view.Select.IndexOf("GROUP BY", StringComparison.Ordinal));
    }

    [Fact]
    public void Daterange_binding_expands_into_half_open_pair()
    {
        var withFilter = WithFilterBefore(ParseFlow(ViewFlow), "daterange", "usage_date");
        // binding: usage_date column, daterange param
        var view = Assert.Single(ManagedViewsEmitter.Emit(withFilter, Catalog()));
        Assert.Contains("@product_from DATE", view.Ddl);
        Assert.Contains("@product_to DATE", view.Ddl);
        Assert.Contains("usage_date >= @product_from AND usage_date < @product_to", view.Select);
    }

    [Fact]
    public void Chain_id_and_row_type_ride_the_view()
    {
        var view = Assert.Single(ManagedViewsEmitter.Emit(ParseFlow(ViewFlow), Catalog()));
        Assert.Equal("by_app", view.ChainId);
        // ADR-0127 inference filled the grouped output type at parse time
        Assert.Equal("EventSummaryRow", view.RowType);
    }

    // === ADR-0126 slice 3: composite sources compose into SQL ===

    private const string JoinFlow = """
        module flow usage_join view

        source events = View(dbo.events) {
          use provider infer
        }

        source users = View(dbo.users) {
          use provider infer
        }

        source joined = InnerJoin(events, users) {
          on = user_id
        }

        end flow
        """;

    [Fact]
    public void Composite_join_emits_native_join_from()
    {
        var views = ManagedViewsEmitter.Emit(ParseFlow(JoinFlow), Catalog());
        var joined = Assert.Single(views, v => v.Select.Contains("JOIN"));

        Assert.Contains("FROM dbo.events AS l INNER JOIN dbo.users AS r", joined.Select);
        Assert.Contains("ON l.user_id = r.user_id", joined.Select);
        Assert.StartsWith("dashspec_usage_join_joined_", joined.ViewName);
    }

    [Fact]
    public void Composite_join_kind_maps_to_sql_kind()
    {
        var flow = ParseFlow(JoinFlow.Replace("InnerJoin(events, users)", "LeftJoin(events, users)"));
        var joined = Assert.Single(ManagedViewsEmitter.Emit(flow, Catalog()), v => v.Select.Contains("JOIN"));
        Assert.Contains("LEFT JOIN", joined.Select);
    }

    private const string UnionFlow = """
        module flow usage_union view

        source e1 = View(dbo.events_2025) {
          use provider infer
        }

        source e2 = View(dbo.events_2026) {
          use provider infer
        }

        source all_events = UnionAll(e1, e2) {
        }

        end flow
        """;

    [Fact]
    public void Composite_union_wraps_into_derived_table()
    {
        var views = ManagedViewsEmitter.Emit(ParseFlow(UnionFlow), Catalog());
        var union = Assert.Single(views, v => v.Select.Contains("UNION ALL"));

        Assert.Contains("FROM (\nSELECT * FROM dbo.events_2025\nUNION ALL\nSELECT * FROM dbo.events_2026\n) AS u", union.Select);
        Assert.StartsWith("dashspec_usage_union_all_events_", union.ViewName);
    }

    [Fact]
    public void Bound_filter_over_composite_join_rides_an_itvf()
    {
        var parsed = ParseFlow(JoinFlow);
        var withFilter = parsed
            .WithTransformers([.. parsed.Transformers, FilterNode("string", "l.app_name")])
            .WithLinks(
            [
                new DashSpec.Modeling.Parse.DataFlow.FlowLinkDef(
                    "joined",
                    "rows",
                    "filter.product",
                    "rows"),
            ]);

        var view = Assert.Single(ManagedViewsEmitter.Emit(withFilter, Catalog()), v => v.IsFunction);
        Assert.Contains("INNER JOIN", view.Select);
        Assert.Contains("WHERE l.app_name = @product", view.Select);
        Assert.StartsWith("CREATE OR ALTER FUNCTION", view.Ddl);
    }
}
