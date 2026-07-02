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
            @runtime "demo.toml"

            @dashboard t
            dashboard "T" {
              card a as "A" {
                diagram number { value = x }
                datasource view dbo.t
              }
            }
            """;

        Assert.Equal("demo.toml", DashSpecParser.ReadRuntimePath(text));
        Assert.Equal("demo.toml", DashSpecParser.ReadConfigPath(text));
        Assert.Equal(("t", "T"), DashSpecParser.ReadDashboardHeader(text));
        Assert.Equal("t", DashSpecParser.Parse(text).Id);
    }

    [Fact]
    public void ReadConfigPath_accepts_deprecated_alias()
    {
        const string text = """
            @config "legacy.toml"

            @dashboard t
            dashboard "T" {
              card a as "A" {
                diagram number { value = x }
                datasource view dbo.t
              }
            }
            """;

        Assert.Equal("legacy.toml", DashSpecParser.ReadRuntimePath(text));
    }

    [Fact]
    public void ReadSqlDialect_parses_file_directive()
    {
        const string text = """
            @runtime "cfg.toml"
            @sqldialect postgres

            @dashboard t
            dashboard "T" {
              card a as "A" {
                diagram number { value = x }
                datasource view dbo.t
              }
            }
            """;

        Assert.Equal(SqlDialect.Postgres, DashSpecParser.ReadSqlDialect(text));
        Assert.Equal(SqlDialect.Postgres, DashSpecParser.Parse(text).SqlDialect);
    }

}
