using DashSpec.Core.Model;
using DashSpec.Execution.Parsing;
using Xunit;

namespace DashSpec.Execution.Core.Tests;

public sealed class DashSpecParserFacadeTests
{
    [Fact]
    public void Parse_dashboard_via_execution_facade_matches_core_entry()
    {
        const string text = """
            @dashboard t
              runtime
              manifest = "cfg.toml"
              end runtime
              report
              title = "T"
              card a as "A"
              diagram number
              value = x
              end number
              datasource view dbo.t
              end card
              end report
            end dashboard
            """;

        var executionDoc = DashSpecParser.Parse(text);
        var coreDoc = DashSpec.Core.Parsing.DashSpecParser.Parse(text);

        Assert.Equal(coreDoc.Id, executionDoc.Id);
        Assert.Equal(coreDoc.Cards.Count, executionDoc.Cards.Count);
        Assert.Equal(coreDoc.Cards[0].Id, executionDoc.Cards[0].Id);
    }

    [Fact]
    public void ReadSqlDialect_via_execution_facade()
    {
        const string text = """
            @dashboard t
              runtime
              manifest = "cfg.toml"
              end runtime
              configuration
              sqldialect = postgres
              end configuration
              report
              title = "T"
              card a as "A"
              diagram number
              value = x
              end number
              datasource view dbo.t
              end card
              end report
            end dashboard
            """;

        Assert.Equal(SqlDialect.Postgres, DashSpecParser.ReadSqlDialect(text));
    }
}
