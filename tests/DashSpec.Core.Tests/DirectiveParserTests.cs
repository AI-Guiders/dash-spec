using DashSpec.Abstractions.Query;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class DirectiveParserTests
{
    [Fact]
    public void ReadRuntimePath_returns_relative_toml_path()
    {
        const string text = """
            @dashboard t
              runtime
              manifest = "demo.toml"
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

        Assert.Equal("demo.toml", DashSpecParser.ReadRuntimePath(text));
        Assert.Equal("demo.toml", DashSpecParser.ReadConfigPath(text));
        var doc = DashSpecParser.Parse(text);
        Assert.Equal("t", doc.Id);
        Assert.Equal("T", doc.Title);
        Assert.Equal("t", DashSpecParser.Parse(text).Id);
    }

    [Fact]
    public void ReadConfigPath_accepts_deprecated_alias()
    {
        const string text = """
            @dashboard t
              runtime
              manifest = "legacy.toml"
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

        Assert.Equal("legacy.toml", DashSpecParser.ReadRuntimePath(text));
    }

    [Fact]
    public void ReadSqlDialect_parses_file_directive()
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
        Assert.Equal(SqlDialect.Postgres, DashSpecParser.Parse(text).SqlDialect);
    }

}
