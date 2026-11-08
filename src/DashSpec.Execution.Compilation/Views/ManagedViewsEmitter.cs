using System.Security.Cryptography;
using System.Text;
using DashSpec.Modeling.Core;
using DashSpec.Modeling.Parse.DataFlow;
using DashSpec.Execution.Runtime;

namespace DashSpec.Execution.Compilation.Views;

/// <summary>One emitted managed view (ADR-0125): a deterministic content-addressed
/// name, the SELECT it is a function of, and the dialect DDL.</summary>
public sealed record ManagedView(string ViewName, string Select, string Ddl);

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
            var from = ResolveFrom(source);
            if (from is null)
            {
                continue; // xlsx / sql-file chains do not emit views (v1)
            }

            foreach (var chain in ChainsFrom(module, source.Id))
            {
                var select = BuildSelect(module, source, chain, from, catalog);
                if (select is null)
                {
                    continue;
                }

                var chainId = chain.Count > 0 ? chain[^1].Id : source.Id;
                var name = $"dashspec_{Slug(module.FlowId)}_{Slug(chainId)}_{Sha12(select)}";
                views.Add(new ManagedView(name, select, DdlFor(name, select, dialect)));
            }
        }

        return views;
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

    private static string? ResolveFrom(DashflowSourceDef source)
    {
        if (source.From.Kind.Equals(SourceFromKind.View))
        {
            return source.From.Value;
        }

        if (source.From.Kind.Equals(SourceFromKind.SqlQuery))
        {
            return $"(\n{source.From.Value}\n)";
        }

        return null;
    }

    private static string? BuildSelect(
        DashflowModule module,
        DashflowSourceDef source,
        List<DashflowTransformerDef> chain,
        string from,
        TypeCatalog catalog)
    {
        var columns = ColumnList(source, catalog);
        var current = $"SELECT {columns}\nFROM {from}";

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
            .Select(f => f.Name)
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
}
