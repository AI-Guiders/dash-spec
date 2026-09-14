namespace DashSpec.Modeling.Parse.Document

open System
open DashSpec.Modeling.Core

module SqlDialectParser =

    let parse (raw: string) =
        if String.IsNullOrWhiteSpace raw then
            invalidArg "raw" "SQL dialect value is required."

        match raw.Trim().ToLowerInvariant() with
        | "tsql" | "mssql" | "sqlserver" -> SqlDialect.TSql
        | "postgres" | "postgresql" | "pg" -> SqlDialect.Postgres
        | "generic" -> SqlDialect.Generic
        | _ ->
            raise (DashSpecParseException($"Unknown @sqldialect '{raw}'. Expected: tsql, postgres, generic."))
