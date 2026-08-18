using DashSpec.Abstractions.Query;
using DashSpec.Core.Compilation;
using DashSpec.Core.Layout;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class SqlDataSourceParserTests
{
    [Fact]
    public void Parse_sql_datasource_reads_inline_select()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
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
              diagram bar
              x = user_sam y
              end bar
              datasource sql query "SELECT user_sam, peak FROM demo.v_x GROUP BY user_sam"
              end card
              end report
            end dashboard
""");

        var card = doc.Cards[0];
        Assert.Equal(DataSourceKind.Sql, card.DataSource.Kind);
        Assert.Equal(DataSourceSqlCarrier.Query, card.DataSource.SqlCarrier);
        Assert.Contains("GROUP BY", card.DataSource.Value);
    }

    [Theory]
    [InlineData("DELETE FROM t")]
    [InlineData("SELECT 1; DROP TABLE t")]
    [InlineData("INSERT INTO t SELECT 1")]
    [InlineData("SELECT * INTO hack FROM t")]
    [InlineData("SELECT 1 -- evil")]
    public void Parse_sql_datasource_rejects_non_readonly(string sqlBody)
    {
        var spec = $$"""
            @dashboard t
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
              diagram bar
              x = a
              y = b
              end bar
              datasource sql query "{{sqlBody.Replace("\"", "\\\"")}}"
              end card
              end report
            end dashboard
            """;

        var ex = Assert.ThrowsAny<Exception>(() => DashSpecParser.Parse(spec));
        Assert.Contains("datasource sql", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_sql_datasource_allows_keyword_inside_string_literal()
    {
        var doc = DashSpecParser.Parse("""
            @dashboard t
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
              diagram bar
              x = title y
              end bar
              datasource sql query "SELECT title FROM t WHERE title = 'DELETE is ok'"
              end card
              end report
            end dashboard
""");

        Assert.Equal(DataSourceKind.Sql, doc.Cards[0].DataSource.Kind);
        Assert.Equal(DataSourceSqlCarrier.Query, doc.Cards[0].DataSource.SqlCarrier);
    }

    [Fact]
    public void Parse_sql_datasource_rejects_bare_string_without_query_or_file()
    {
        var ex = Assert.Throws<DashSpecParseException>(() => DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              card a as "A"
              diagram bar
              x = a y
              end bar
              datasource sql "SELECT 1"
              end card
              end report
            end dashboard
"""));

        Assert.Contains("query' or 'file'", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_sql_datasource_file_and_block_query()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-sql-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var sqlPath = Path.Combine(dir, "queries", "top.sql");
        Directory.CreateDirectory(Path.GetDirectoryName(sqlPath)!);
        File.WriteAllText(sqlPath, "SELECT user_sam, MAX(n) AS peak FROM t GROUP BY user_sam");

        try
        {
            var fileDoc = DashSpecParser.Parse("""
                @dashboard t
                  report
                  title = "T"
                  card a as "A"
                  diagram bar
                  x = user_sam y
                  end bar
                  datasource sql file "queries/top.sql"
                  end card
                  end report
                end dashboard
""", dir);

            var fileCard = fileDoc.Cards[0];
            Assert.Equal(DataSourceSqlCarrier.File, fileCard.DataSource.SqlCarrier);
            Assert.Equal("queries/top.sql", fileCard.DataSource.Value);

            var blockDoc = DashSpecParser.Parse("""
                @dashboard t
                  report
                  title = "T"
                  card b as "B"
                  diagram bar
                  x = user_sam y
                  end bar
                  datasource sql query [[
                  SELECT user_sam, COUNT(*) AS peak
                  FROM t
                  GROUP BY user_sam
                  ]]
                  end card
                  end report
                end dashboard
""", dir);

            var blockCard = blockDoc.Cards[0];
            Assert.Equal(DataSourceSqlCarrier.Query, blockCard.DataSource.SqlCarrier);
            Assert.Contains("COUNT(*)", blockCard.DataSource.Value);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

}
