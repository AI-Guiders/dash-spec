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
}
