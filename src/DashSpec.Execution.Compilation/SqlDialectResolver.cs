using DashSpec.Abstractions.Query;
using DashSpec.Execution.Compilation.Dialects;

namespace DashSpec.Execution.Compilation;

public static class SqlDialectResolver
{
    public static ISqlDialectBackend Resolve(SqlDialect dialect)
    {
        SqlDialectBackendBootstrap.EnsureRegistered();
        return SqlDialectRegistry.Resolve(ToRegistryId(dialect));
    }

    public static string ToRegistryId(SqlDialect dialect)
    {
        if (dialect == SqlDialect.TSql) return "tsql";
        if (dialect == SqlDialect.Postgres) return "postgres";
        if (dialect == SqlDialect.Generic) return "generic";
        return "tsql";
    }

    public static SqlDialect ParseRegistryId(string id) =>
        id.ToLowerInvariant() switch
        {
            "tsql" or "mssql" or "sqlserver" => SqlDialect.TSql,
            "postgres" or "postgresql" or "pg" => SqlDialect.Postgres,
            "generic" => SqlDialect.Generic,
            _ => SqlDialect.TSql,
        };
}
