using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class SqlDialectParser
{
    public static SqlDialect Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);

        return raw.Trim().ToLowerInvariant() switch
        {
            "tsql" or "mssql" or "sqlserver" => SqlDialect.TSql,
            "postgres" or "postgresql" or "pg" => SqlDialect.Postgres,
            "generic" => SqlDialect.Generic,
            _ => throw new DashSpecParseException(
                $"Unknown @sqldialect '{raw}'. Expected: tsql, postgres, generic."),
        };
    }
}
