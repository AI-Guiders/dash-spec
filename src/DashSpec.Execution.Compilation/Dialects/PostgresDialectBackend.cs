using DashSpec.Abstractions.Query;

namespace DashSpec.Execution.Compilation.Dialects;

public sealed class PostgresDialectBackend : TSqlDialectBackend
{
    public override string Id => "postgres";

    public override SqlRowLimitStyle RowLimitStyle => SqlRowLimitStyle.TrailingLimit;

    public override string FormatDateRangeUpperExclusive(string toParameter) =>
        $"({toParameter}::date + INTERVAL '1 day')";
}
