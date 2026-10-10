using DashSpec.Abstractions.Query;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Compilation;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

/// <summary>
/// Flow source contract (from sql query/file) — source shape is validated at parse,
/// the read-only guard (SqlReadOnlyValidator, "datasource sql" messages) runs at compile.
/// </summary>
public class SqlDataSourceParserTests
{
    private static string Spec(string moduleName, string sourceId) =>
        $$"""
        @dashboard t
              !include "query-row-types.dashtype"
          connect
          flow "{{moduleName}}"
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
              label = "Дата"
            end show
          end filter
          filters dashboard
          usage_date
          end dashboard
          card a as "A"
          bind
            usage_date
          end bind
          diagram bar
          x = a y
          end bar
          data flow { {{sourceId}} [rows] -> [rows] __diagram__ }
          end card
          end report
        end dashboard
        """;

    [Fact]
    public void Parse_sql_datasource_reads_inline_select()
    {
        var card = DashSpecTestRowTypes.ParseDashboard(Spec("fixture.dashflow", "s_wrap")).Cards[0];
        Assert.Equal(DataSourceKind.Sql, card.DataSource.Kind);
        Assert.Equal(DataSourceSqlCarrier.Query, card.DataSource.SqlCarrier);
        Assert.Contains("GROUP BY", card.DataSource.Value);
    }

    [Theory]
    [InlineData("s_delete")]
    [InlineData("s_drop")]
    [InlineData("s_insert")]
    [InlineData("s_into")]
    [InlineData("s_comment")]
    public void Compile_sql_datasource_rejects_non_readonly(string sourceId)
    {
        var document = DashSpecTestRowTypes.ParseDashboard(Spec("fixture.dashflow", sourceId));
        var card = document.Cards[0];
        var ex = Assert.ThrowsAny<Exception>(() =>
            QueryCompiler.Compile(card, new FilterState(), new Dictionary<string, Model.FilterDefinition>(), document));
        Assert.Contains("datasource sql", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_sql_datasource_allows_keyword_inside_string_literal()
    {
        var document = DashSpecTestRowTypes.ParseDashboard(Spec("fixture.dashflow", "s_kw"));
        var card = document.Cards[0];
        Assert.Contains("DELETE is ok", card.DataSource.Value);

        var query = QueryCompiler.Compile(card, new FilterState(), new Dictionary<string, Model.FilterDefinition>(), document);
        Assert.Contains("DELETE is ok", query.Sql);
    }

    [Fact]
    public void Parse_sql_datasource_rejects_bare_string_without_query_or_file()
    {
        var dir = NewSpecDir();
        File.WriteAllText(Path.Combine(dir, "broken.dashflow"), """
            @flow broken
            source s {
              use provider infer
              from sql "SELECT 1"
              ports
                default output rows
                output stream rows: FixtureRow
              end ports
            }
            end flow
            """);

        var ex = Assert.Throws<DashSpecParseException>(() =>
            DashSpecTestRowTypes.ParseDashboard(Spec("broken.dashflow", "s"), dir));
        Assert.Contains("query or file", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_sql_datasource_file_and_block_query()
    {
        var dir = NewSpecDir();
        var sqlPath = Path.Combine(dir, "queries", "top.sql");
        Directory.CreateDirectory(Path.GetDirectoryName(sqlPath)!);
        File.WriteAllText(sqlPath, "SELECT user_sam, MAX(n) AS peak FROM t GROUP BY user_sam");
        File.WriteAllText(Path.Combine(dir, "src.dashflow"), """
            @flow src
            source s_file {
              use provider infer
              from sql file "queries/top.sql"
              ports
                default output rows
                output stream rows: FixtureRow
              end ports
            }

            source s_block {
              use provider infer
              from sql query [[
              SELECT user_sam, COUNT(*) AS peak
              FROM t
              GROUP BY user_sam
              ]]
              ports
                default output rows
                output stream rows: FixtureRow
              end ports
            }
            end flow
            """);

        var fileCard = DashSpecTestRowTypes.ParseDashboard(Spec("src.dashflow", "s_file"), dir).Cards[0];
        Assert.Equal(DataSourceSqlCarrier.File, fileCard.DataSource.SqlCarrier);
        Assert.Equal("queries/top.sql", fileCard.DataSource.Value);

        var blockCard = DashSpecTestRowTypes.ParseDashboard(Spec("src.dashflow", "s_block"), dir).Cards[0];
        Assert.Equal(DataSourceSqlCarrier.Query, blockCard.DataSource.SqlCarrier);
        Assert.Contains("COUNT(*)", blockCard.DataSource.Value);
    }

    private static string NewSpecDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-sql-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        DashSpecTestRowTypes.SeedFixtureTypesDirectory(dir);
        return dir;
    }
}
