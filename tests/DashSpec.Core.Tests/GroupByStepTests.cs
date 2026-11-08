using DashSpec.Execution.Compilation;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

/// <summary>ADR-0121/0126: a summarize database-transform step compiles into the slot's
/// SQL — branch into a separate port, aggregation stays in the database.</summary>
public class GroupByStepTests
{
    private static (DashboardDocument Document, CardDefinition Card) Card()
    {
        var document = DashSpecTestRowTypes.ParseDashboard("""
            module dashboard t
                  import types from Fixtures.QueryRowTypes
                  connect
                    use flow fixture_flow
                  end connect
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
                  label = "Usage"
                end show
              end filter
              card grouped as "Grouped"
              diagram table
                columns = app_name
                aggregate = count
              end table
              data flow { fixture_src [rows] -> [rows] __diagram__ }
              layout
                [ __diagram__ ]
              end layout
              end card
              end report
            end dashboard
""");
        return (document, document.Cards[0]);
    }

    [Fact]
    public void Compile_pushes_summarize_into_sql()
    {
        var (document, card) = Card();
        var query = QueryCompiler.Compile(
            card,
            new FilterState(),
            new Dictionary<string, FilterDefinition>(),
            document,
            groupByStep: new GroupByStepSpec("app_name", "count", null));

        Assert.Contains("app_name AS app_name", query.Sql);
        Assert.Contains("COUNT(*) AS value", query.Sql);
        Assert.Contains("GROUP BY app_name", query.Sql);
    }

    [Fact]
    public void Compile_uses_the_aggregate_argument_column()
    {
        var (document, card) = Card();
        var query = QueryCompiler.Compile(
            card,
            new FilterState(),
            new Dictionary<string, FilterDefinition>(),
            document,
            groupByStep: new GroupByStepSpec("app_name", "sum", "peak_concurrent_proxy"));

        Assert.Contains("SUM(peak_concurrent_proxy) AS value", query.Sql);
        Assert.Contains("GROUP BY app_name", query.Sql);
    }

    [Fact]
    public void Parse_is_loud_about_a_missing_by()
    {
        var ex = Assert.Throws<DashSpec.Execution.Runtime.DashSpecParseException>(() =>
            GroupByStepSpec.Parse([new Modeling.Parse.DataFlow.DashflowTransformParam("agg", "count")]));
        Assert.Contains("'by'", ex.Message);
    }

    [Fact]
    public void Parse_is_loud_about_an_unknown_aggregate()
    {
        var ex = Assert.Throws<DashSpec.Execution.Runtime.DashSpecParseException>(() =>
            GroupByStepSpec.Parse(
            [
                new Modeling.Parse.DataFlow.DashflowTransformParam("by", "app_name"),
                new Modeling.Parse.DataFlow.DashflowTransformParam("agg", "median"),
            ]));
        Assert.Contains("median", ex.Message);
    }

    [Fact]
    public void Parse_is_loud_about_a_missing_value_for_a_valued_aggregate()
    {
        var ex = Assert.Throws<DashSpec.Execution.Runtime.DashSpecParseException>(() =>
            GroupByStepSpec.Parse([new Modeling.Parse.DataFlow.DashflowTransformParam("by", "app_name"), new("agg", "avg")]));
        Assert.Contains("'value'", ex.Message);
    }

    [Fact]
    public void Find_returns_null_for_a_path_without_the_step()
    {
        Assert.Null(GroupByStepSpec.Find(null));
    }
}
