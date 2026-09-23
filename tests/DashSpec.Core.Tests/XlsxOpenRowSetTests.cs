using DashSpec.Abstractions.Query;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Execution.Compilation;
using DashSpec.Execution.Runtime;
using Xunit;

namespace DashSpec.Core.Tests;

public class XlsxOpenRowSetTests
{
    [Fact]
    public void Compile_xlsx_datasource_emits_openrowset()
    {
        var card = DashSpecParser.Parse("""
            @dashboard t
              configuration
              sqldialect = tsql
              end configuration
              report
              title = "T"
              card sheet as "Sheet"
              diagram table
              columns = app_name, seats
              end table
              datasource xlsx file "reports/book.xlsx" sheet "Лист1"
              end card
              end report
            end dashboard
            """).Cards[0];

        var query = QueryCompiler.Compile(
            card,
            new FilterState(),
            new Dictionary<string, FilterDefinition>(),
            SqlDialect.TSql,
            specDirectory: @"D:\specs");

        Assert.Contains("OPENROWSET('Microsoft.ACE.OLEDB.12.0'", query.Sql);
        Assert.Contains(@"Database=D:\specs\reports\book.xlsx", query.Sql);
        Assert.Contains("SELECT * FROM [Лист1$]", query.Sql);
        Assert.Contains("AS xlsx_src", query.Sql);
    }

    [Fact]
    public void Compile_xlsx_datasource_rejects_non_tsql()
    {
        var card = DashSpecParser.Parse("""
            @dashboard t
              report
              title = "T"
              card sheet as "Sheet"
              diagram table
              columns = app_name
              end table
              datasource xlsx file "reports/book.xlsx"
              end card
              end report
            end dashboard
            """).Cards[0];

        var error = Assert.Throws<InvalidOperationException>(() =>
            QueryCompiler.Compile(
                card,
                new FilterState(),
                new Dictionary<string, FilterDefinition>(),
                SqlDialect.Postgres));

        Assert.Contains("OPENROWSET", error.Message);
    }
}
