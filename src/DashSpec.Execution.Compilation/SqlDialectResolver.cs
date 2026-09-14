using DashSpec.Abstractions.Query;
using DashSpec.Execution.Compilation.Dialects;
using DashSpec.Core.Model;

namespace DashSpec.Execution.Compilation;

public static class SqlDialectResolver
{
    public static ISqlDialectBackend Resolve(SqlDialect dialect)
    {
        SqlDialectBackendBootstrap.EnsureRegistered();
        return SqlDialectRegistry.Resolve(ToRegistryId(dialect));
    }

    public static string ToRegistryId(SqlDialect dialect) =>
        dialect switch
        {
            SqlDialect.TSql => "tsql",
            SqlDialect.Postgres => "postgres",
            SqlDialect.Generic => "generic",
            _ => "tsql",
        };

    public static SqlDialect ParseRegistryId(string id) =>
        id.ToLowerInvariant() switch
        {
            "tsql" or "mssql" or "sqlserver" => SqlDialect.TSql,
            "postgres" or "postgresql" or "pg" => SqlDialect.Postgres,
            "generic" => SqlDialect.Generic,
            _ => SqlDialect.TSql,
        };
}
