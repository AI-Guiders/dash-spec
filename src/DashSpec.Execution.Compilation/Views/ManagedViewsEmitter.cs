using System.Security.Cryptography;
using System.Text;
using DashSpec.Modeling.Core;
using DashSpec.Modeling.Parse.DataFlow;
using DashSpec.Execution.Runtime;

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
/// Every source-chain (source → database transforms → terminal node) becomes one
/// view; the physical name is content-addressed — <c>dashspec_&lt;flow&gt;_&lt;chain&gt;_&lt;ast_sha12&gt;</c>
/// (12 hex chars of SHA-256 over the canonical SELECT text), so two authors with
/// the same human names but different logic can never overwrite each other, and
/// identical queries safely share one physical view.
///
/// Runtime transforms (to_zone, group, sort…) are post-SQL by the layering
/// invariant — they never enter the view. The flat VIEW boundary (ADR-0125 §3):
/// filters that must apply BEFORE aggregation need a parameterized artifact
/// (iTVF); the flat view carries only the unfiltered chain, and the reader
/// applies card filters as an outer WHERE (predicate pushdown).
/// </summary>
public static class ManagedViewsEmitter
{
    public static IReadOnlyList<ManagedView> Emit(DashflowModule module, TypeCatalog catalog, string dialect = "tsql")
    {
        var views = new List<ManagedView>();
        if (!module.EmitViews)
        {
            return views;
        }

        foreach (var source in module.Sources)
        {
            var from = ResolveFrom(module, source, catalog);
            if (from is null)
            {
                continue; // xlsx / sql-file chains do not emit views (v1)
            }

            foreach (var chain in ChainsFrom(module, source.Id))
            {
                var chainId = chain.Count > 0 ? chain[^1].Id : source.Id;
                var rowType = ChainRowType(module, source, chain);

                // A parametrized node with a column binding (ADR-0120 filter port +
                // ADR-0125 §3 binding) turns the chain into an iTVF: the filter applies
                // BEFORE aggregation inside the database, as an inlinable function.
                // An UNBOUND param port is a runtime filter with nowhere to push —
                // the §3 boundary is loud: neither a flat VIEW nor an iTVF can carry it.
                var allParams = chain.SelectMany(t => t.Params).ToList();
                var boundParams = allParams
                    .Where(p => p.ColumnBinding is { Value: not null and not "" })
                    .ToList();

                if (allParams.Count > boundParams.Count)
                {
                    var unbound = allParams.First(p => p.ColumnBinding is not { Value: not null and not "" });
                    throw new InvalidOperationException(
                        $"flow '{module.FlowId}': param port '{unbound.Name}' has no column binding — a flat VIEW " +
                        "cannot carry a runtime filter and an inline function requires the bound column. " +
                        "Add 'bind <column>' to the param port or remove the param port from the view chain.");
                }

                if (boundParams.Count > 0)
                {
                    var (parameters, where) = BuildParameters(boundParams);
                    var select = BuildSelect(module, source, chain, from, catalog, where)
                        ?? throw new InvalidOperationException(
                            $"flow '{module.FlowId}': parameterized chain from '{source.Id}' failed to compile.");
                    var name = $"dashspec_{Slug(module.FlowId)}_{Slug(chainId)}_{Sha12(select)}";
                    views.Add(new ManagedView(name, module.FlowId, chainId, rowType, select, FunctionDdl(name, parameters, select, dialect))
                    {
                        IsFunction = true,
                    });
                    continue;
                }

                var flatSelect = BuildSelect(module, source, chain, from, catalog, whereClause: null)
                    ?? throw new InvalidOperationException(
                        $"flow '{module.FlowId}': chain from source '{source.Id}' cannot compile into a flat VIEW " +
                        "(add 'bind <column>' to parameterized ports in the chain).");

                var flatName = $"dashspec_{Slug(module.FlowId)}_{Slug(chainId)}_{Sha12(flatSelect ?? throw new InvalidOperationException("unreachable — null handled above"))}";
                views.Add(new ManagedView(flatName, module.FlowId, chainId, rowType, flatSelect, DdlFor(flatName, flatSelect, dialect)));
            }
        }

        return views;
    }

    /// <summary>The chain's terminal row type — the inferred (ADR-0127) or declared
    /// output of the last transformer; the source type for raw chains.</summary>
    private static string? ChainRowType(DashflowModule module, DashflowSourceDef source, List<DashflowTransformerDef> chain)
    {
        if (chain.Count == 0)
        {
            return source.OutputRowType;
        }

        return chain[^1].Outputs.Length > 0 ? chain[^1].Outputs[0].Item2 : null;
    }

    /// <summary>iTVF parameters + the WHERE fragment they produce. daterange ports
    /// expand into a half-open [from, to) pair — the same shape the reader pushes.</summary>
    private static (IReadOnlyList<ManagedViewParam> Parameters, string Where) BuildParameters(
        IReadOnlyList<DashSpec.Modeling.Parse.DataFlow.DashflowParamPortDef> bound)
    {
        var parameters = new List<ManagedViewParam>();
        var predicates = new List<string>();

        foreach (var port in bound)
        {
            var column = port.ColumnBinding.Value.Trim();
            var name = Slug(port.Name);

            switch (port.ValueType)
            {
                case "daterange":
                    parameters.Add(new ManagedViewParam($"{name}_from", "DATE", column, "range"));
                    parameters.Add(new ManagedViewParam($"{name}_to", "DATE", column, "range"));
                    predicates.Add($"{column} >= @{name}_from AND {column} < @{name}_to");
                    break;
                case "int":
                    parameters.Add(new ManagedViewParam(name, "INT", column, "eq"));
                    predicates.Add($"{column} = @{name}");
                    break;
                default:
                    parameters.Add(new ManagedViewParam(name, "NVARCHAR(400)", column, "eq"));
                    predicates.Add($"{column} = @{name}");
                    break;
            }
        }

        return (parameters, string.Join(" AND ", predicates));
    }

    /// <summary>All transformer paths from a source to terminal nodes (each terminal = one view).</summary>
    private static List<List<DashflowTransformerDef>> ChainsFrom(DashflowModule module, string sourceId)
    {
        var chains = new List<List<DashflowTransformerDef>>();
        Walk(module, sourceId, [], chains);
        return chains;
    }

    private static void Walk(
        DashflowModule module,
        string nodeId,
        List<DashflowTransformerDef> path,
        List<List<DashflowTransformerDef>> chains)
    {
        var next = module.Links
            .Where(link => string.Equals(link.FromNode, nodeId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (next.Count == 0)
        {
            chains.Add([.. path]);
            return;
        }

        foreach (var link in next)
        {
            var transformer = module.Transformers.FirstOrDefault(t =>
                string.Equals(t.Id, link.ToNode, StringComparison.OrdinalIgnoreCase));

            if (transformer is null)
            {
                continue; // nested-flow consumer: not a view chain terminal (v1)
            }

            path.Add(transformer);
            Walk(module, transformer.Id, path, chains);
            path.RemoveAt(path.Count - 1);
        }
    }

    private static string? ResolveFrom(DashflowModule module, DashflowSourceDef source, TypeCatalog catalog)
    {
        if (source.Origin is SourceOrigin.Leaf leaf)
        {
            if (leaf.kind.Equals(SourceFromKind.View))
            {
                return leaf.value;
            }

            if (leaf.kind.Equals(SourceFromKind.SqlQuery))
            {
                return $"(\n{leaf.value}\n)";
            }

            // SqlFile / Xlsx leaves are not managed-view material (as before).
            return null;
        }

        // ADR-0126 slice 3: composite origins compose into SQL here —
        // join sides aliased l/r, union branches project the union row type
        // (canonical column order — a managed union never rides SELECT *).
        return CompositeSourceSql.FromClause(
                module,
                source,
                source.Origin is SourceOrigin.UnionAll ? ColumnList(source, catalog) : null)
            ?? throw new InvalidOperationException(
                $"Dashflow source '{source.Id}': composite origin has no SQL material (ADR-0126).");
    }

    private static string? BuildSelect(
        DashflowModule module,
        DashflowSourceDef source,
        List<DashflowTransformerDef> chain,
        string from,
        TypeCatalog catalog,
        string? whereClause)
    {
        var columns = source.Origin is SourceOrigin.InnerJoin or SourceOrigin.LeftJoin
            or SourceOrigin.RightJoin or SourceOrigin.FullJoin
            ? CompositeColumnList(module, source, catalog)
            : ColumnList(source, catalog);
        var current = $"SELECT {columns}\nFROM {from}";

        if (whereClause is not null)
        {
            // The filter applies BEFORE aggregation (ADR-0125 §3) — it rides the
            // innermost SELECT over the base table, never the post-aggregate result.
            current = $"{current}\nWHERE {whereClause}";
        }

        foreach (var transformer in chain)
        {
            foreach (var step in transformer.Steps)
            {
                if (!string.Equals(
                        DashSpec.Modeling.Core.TransformDeclarations.localityOf(step.PluginId),
                        "database",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue; // runtime transforms are post-SQL — never in the view
                }

                if (string.Equals(step.PluginId, GroupByStepSpec.PluginIdName, StringComparison.OrdinalIgnoreCase))
                {
                    var group = GroupByStepSpec.Parse(step.Parameters);
                    current =
                        $"SELECT {group.By} AS {group.By}, {group.SqlAggregate} AS value\nFROM (\n{current}\n) AS src\nGROUP BY {group.By}";
                }
                else if (string.Equals(step.PluginId, OrderByStepSpec.PluginIdName, StringComparison.OrdinalIgnoreCase))
                {
                    // A VIEW cannot carry ORDER BY (SQL Server rejects it without TOP,
                    // and result order is not a view's contract anyway). The step is
                    // consumed by the reader: SELECT … FROM view ORDER BY … (ADR-0126:
                    // query order rides the card's query, not the shared artifact).
                    continue;
                }
                else
                {
                    // An unknown database transform cannot be compiled into a flat view —
                    // the chain stays on the reader's graph path (the ADR's escape hatch).
                    return null;
                }
            }
        }

        return current;
    }

    /// <summary>ADR-0126 §2: composite join select list — every column rides a
    /// side-qualified expression over the l/r aliases; the composite row type is the
    /// schema contract (right collisions carry the qualified name r.col). Loud when
    /// the synthesized type is missing — a join view with SELECT * is SQL-invalid.</summary>
    private static string CompositeColumnList(DashflowModule module, DashflowSourceDef source, TypeCatalog catalog)
    {
        var compositeOption = TypeCatalogModule.tryGet(catalog, source.OutputRowType);
        var compositeDef = compositeOption?.Value
            ?? throw new InvalidOperationException(
                $"flow '{module.FlowId}': composite source '{source.Id}' row type '{source.OutputRowType}' is not defined (ADR-0126 §1.1).");

        var (leftId, rightId) = source.Origin switch
        {
            SourceOrigin.InnerJoin j => (j.left, j.right),
            SourceOrigin.LeftJoin j => (j.left, j.right),
            SourceOrigin.RightJoin j => (j.left, j.right),
            SourceOrigin.FullJoin j => (j.left, j.right),
            _ => (null, null)
        };

        var leftDef = RowTypeDefOf(module, catalog, leftId);
        var rightDef = RowTypeDefOf(module, catalog, rightId);

        var parts = new List<string>();
        foreach (var field in compositeDef.Fields.Where(f => f.Type.IsPrimitive))
        {
            if (field.Name.StartsWith("r.", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add($"{field.Name} AS [{field.Name}]");
            }
            else if (leftDef is not null && leftDef.Fields.Any(f => string.Equals(f.Name, field.Name, StringComparison.OrdinalIgnoreCase)))
            {
                parts.Add($"l.{field.Name}");
            }
            else if (rightDef is not null && rightDef.Fields.Any(f => string.Equals(f.Name, field.Name, StringComparison.OrdinalIgnoreCase)))
            {
                parts.Add($"r.{field.Name}");
            }
            else
            {
                parts.Add(field.Name);
            }
        }

        return string.Join(", ", parts);
    }

    private static RowTypeDef? RowTypeDefOf(
        DashflowModule module, TypeCatalog catalog, string? sourceId)
    {
        if (sourceId is null) return null;
        var source = module.Sources.FirstOrDefault(s =>
            string.Equals(s.Id, sourceId, StringComparison.OrdinalIgnoreCase));
        var typeName = source?.OutputRowType;
        if (string.IsNullOrWhiteSpace(typeName)) return null;
        return TypeCatalogModule.tryGet(catalog, typeName)?.Value;
    }

    private static string ColumnList(DashflowSourceDef source, TypeCatalog catalog)
    {
        var defOption = TypeCatalogModule.tryGet(catalog, source.OutputRowType);
        var def = defOption?.Value;
        if (def is null)
        {
            return "*";
        }

        var columns = def.Fields
            .Where(f => f.Type.IsPrimitive)
            .Select(f => f.Name.Contains('.') ? $"{f.Name} AS [{f.Name}]" : f.Name)
            .ToList();

        return columns.Count > 0 ? string.Join(", ", columns) : "*";
    }

    internal static string Sha12(string select)
    {
        var canonical = string.Join(" ", select.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    internal static string Slug(string value)
    {
        var builder = new StringBuilder();
        var lastWasSeparator = false;

        foreach (var ch in value.ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator)
            {
                builder.Append('_');
                lastWasSeparator = true;
            }
        }

        return builder.ToString().TrimEnd('_');
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
