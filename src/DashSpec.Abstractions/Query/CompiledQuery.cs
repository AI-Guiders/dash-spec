using System.Data;
using DashSpec.Abstractions.Data;

namespace DashSpec.Abstractions.Query;

public sealed record CompiledQuery(
    string Sql,
    IReadOnlyList<QueryParameter> Parameters,
    RowTypeSchema RowSchema,
    /// <summary>Хранение времени в источнике: "utc" | "local"; null — не заявлено (по умолчанию UTC).</summary>
    string? DateTimeFrame = null);

public sealed record QueryParameter(string Name, object Value, DbType? DbType = null)
{
    public DbType ResolvedDbType =>
        DbType ?? Value switch
        {
            DateOnly => System.Data.DbType.Date,
            TimeOnly => System.Data.DbType.Time,
            DateTimeOffset => System.Data.DbType.DateTimeOffset,
            DateTime => System.Data.DbType.DateTime2,
            bool => System.Data.DbType.Boolean,
            int => System.Data.DbType.Int32,
            long => System.Data.DbType.Int64,
            double => System.Data.DbType.Double,
            decimal => System.Data.DbType.Decimal,
            Guid => System.Data.DbType.Guid,
            _ => System.Data.DbType.String,
        };
}
