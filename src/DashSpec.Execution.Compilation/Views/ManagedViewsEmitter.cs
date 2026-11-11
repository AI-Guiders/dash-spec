using System.Text;
using DashSpec.Modeling.Core;
using DashSpec.Modeling.Parse.DataFlow;

namespace DashSpec.Execution.Compilation.Views;

/// <summary>One emitted managed view (ADR-0125): a deterministic content-addressed
/// name, the SELECT it is a function of, and the dialect DDL.</summary>
public sealed record ManagedView(
    string ViewName,
    string FlowId,
    string ChainId,
    string? RowType,
    string Select,
    string Ddl)
{
    /// <summary>A parameterized inline table-valued function (the §3 boundary:
    /// a filter must apply BEFORE aggregation — a flat VIEW cannot carry it).</summary>
    public bool IsFunction { get; init; }
}

/// <summary>One filter parameter of an emitted iTVF: the port name, its SQL type,
/// and the bound predicate column.</summary>
public sealed record ManagedViewParam(string Name, string SqlType, string Column, string Match);

/// <summary>
/// ADR-0125: emits <c>module flow &lt;id&gt; view</c> chains into managed views.
/// The name, the canonical SELECT, the terminal row type and the parameter
/// contract are the F# SSOT (<c>ManagedViews</c>, Modeling.Parse) — computed from
/// the parsed .ds module alone. This projection adds only the dialect DDL.
/// </summary>
public static class ManagedViewsEmitter
{
    public static IReadOnlyList<ManagedView> Emit(DashflowModule module, TypeCatalog catalog, string dialect = "tsql")
    {
        var views = new List<ManagedView>();

        foreach (var entry in ManagedViews.emit(module, catalog))
        {
            var parameters = entry.Params
                .Select(p => new ManagedViewParam(p.Item1, p.Item2, p.Item3, p.Item4))
                .ToList();

            var ddl = entry.IsFunction
                ? FunctionDdl(entry.ViewName, parameters, entry.Select, dialect)
                : DdlFor(entry.ViewName, entry.Select, dialect);

            views.Add(new ManagedView(
                entry.ViewName,
                entry.Flow,
                entry.Chain,
                entry.RowType is { } rowType && !string.IsNullOrWhiteSpace(rowType.Value) ? rowType.Value : null,
                entry.Select,
                ddl)
            {
                IsFunction = entry.IsFunction,
            });
        }

        return views;
    }

    private static string DdlFor(string name, string select, string dialect) =>
        dialect.Equals("postgres", StringComparison.OrdinalIgnoreCase)
            ? $"CREATE OR REPLACE VIEW {name} AS\n{select};"
            : $"CREATE OR ALTER VIEW {name} AS\n{select};";

    /// <summary>Inline table-valued function DDL — the inlinable parameterized view
    /// (tsql: RETURNS TABLE AS RETURN; postgres: LANGUAGE sql STABLE function).</summary>
    private static string FunctionDdl(
        string name,
        IReadOnlyList<ManagedViewParam> parameters,
        string select,
        string dialect)
    {
        var signature = string.Join(", ", parameters.Select(p => $"@{p.Name} {p.SqlType}"));

        if (dialect.Equals("postgres", StringComparison.OrdinalIgnoreCase))
        {
            var pgSignature = string.Join(", ", parameters.Select(p => $"{p.Name.Replace("@", "")} {PgType(p.SqlType)}"));
            return $"CREATE OR REPLACE FUNCTION {name}({pgSignature})\nRETURNS TABLE AS $$\n{select}\n$$ LANGUAGE sql STABLE;";
        }

        return $"CREATE OR ALTER FUNCTION {name}({signature})\nRETURNS TABLE\nAS RETURN\n(\n{select}\n);";
    }

    private static string PgType(string sqlType) => sqlType switch
    {
        "INT" => "integer",
        "DATE" => "date",
        _ => "text",
    };
}
