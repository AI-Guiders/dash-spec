using System.Text;
using DashSpec.Abstractions.Query;
using DashSpec.Execution.Compilation;
using DashSpec.Execution.Compilation.Dialects;
using DashSpec.Core.Model;
using Xunit;

namespace DashSpec.Core.Tests;

public sealed class SqlDialectRegistryTests
{
    public SqlDialectRegistryTests()
    {
        _ = SqlDialectResolver.Resolve(SqlDialect.TSql);
    }
    [Theory]
    [InlineData("tsql", typeof(TSqlDialectBackend), SqlRowLimitStyle.TopPrefix)]
    [InlineData("postgres", typeof(PostgresDialectBackend), SqlRowLimitStyle.TrailingLimit)]
    [InlineData("generic", typeof(GenericDialectBackend), SqlRowLimitStyle.TopPrefix)]
    public void Built_in_backends_are_registered(string id, Type expectedType, SqlRowLimitStyle rowLimitStyle)
    {
        var backend = SqlDialectRegistry.Resolve(id);

        Assert.IsType(expectedType, backend);
        Assert.Equal(rowLimitStyle, backend.RowLimitStyle);
    }

    [Fact]
    public void Postgres_backend_formats_interval_upper_bound()
    {
        var backend = SqlDialectRegistry.Resolve("postgres");

        Assert.Equal("(@usage_date_to::date + INTERVAL '1 day')", backend.FormatDateRangeUpperExclusive("@usage_date_to"));
    }

    [Fact]
    public void Postgres_backend_appends_trailing_limit()
    {
        var backend = SqlDialectRegistry.Resolve("postgres");
        var sql = new StringBuilder();

        backend.AppendSelect(
            sql,
            new SqlSelectParts("id, name", "dbo.events", "WHERE 1=1", null, "ORDER BY id", 100));

        Assert.EndsWith("LIMIT 100", sql.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("TOP", sql.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
