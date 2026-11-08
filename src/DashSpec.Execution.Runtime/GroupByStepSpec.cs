using DashSpec.Modeling.Core;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// ADR-0121: a <c>group_by</c> database-transform step riding the slot's flow path.
/// The step compiles into the slot's SQL — <c>SELECT by, AGG(value) … GROUP BY by</c> —
/// and never executes in memory (the batch executor sees it as a no-op, like the
/// timeshift metadata steps). Branch, not mutation: the raw sibling slot keeps its
/// own ungrouped query.
/// </summary>
public sealed record GroupByStepSpec(string By, string? Agg, string? Value)
{
    public const string PluginIdName = "group_by";

    private const string AggregateColumn = "value";

    private static readonly string[] KnownAggregates = ["count", "sum", "max", "min", "avg"];

    /// <summary>First <c>group_by</c> step with an explicit <c>agg</c> — the database face
    /// (ADR-0123 §4: agg present ⇒ SQL summary; agg absent ⇒ runtime grouping, which does
    /// not ride the query at all and is handled by the batch executor).</summary>
    public static GroupByStepSpec? Find(DashflowExecutionPath? path)
    {
        if (path is null)
        {
            return null;
        }

        foreach (var transformer in path.Transformers)
        {
            foreach (var step in transformer.Steps)
            {
                if (string.Equals(step.PluginId, PluginIdName, StringComparison.OrdinalIgnoreCase))
                {
                    var spec = Parse(step.Parameters);
                    return spec.Agg is null ? null : spec;
                }
            }
        }

        return null;
    }

    /// <summary>Loud parameter contract: <c>by</c> required, <c>agg</c> one of
    /// count|sum|max|min|avg (default count), <c>value</c> required for every
    /// aggregate except count.</summary>
    public static GroupByStepSpec Parse(Modeling.Parse.DataFlow.DashflowTransformParam[] parameters)
    {
        string? by = null, agg = null, value = null;

        foreach (var parameter in parameters)
        {
            if (string.Equals(parameter.Key, "by", StringComparison.OrdinalIgnoreCase))
            {
                by = parameter.Value;
            }
            else if (string.Equals(parameter.Key, "agg", StringComparison.OrdinalIgnoreCase))
            {
                agg = parameter.Value;
            }
            else if (string.Equals(parameter.Key, "value", StringComparison.OrdinalIgnoreCase))
            {
                value = parameter.Value;
            }
        }

        if (string.IsNullOrWhiteSpace(by))
        {
            throw new DashSpecParseException("group_by: the 'by' parameter is required (the grouping column).");
        }

        // ADR-0123 §4: agg absent ⇒ the runtime face (grouping, rows stay rows) — no
        // default aggregate is invented here; only an explicit agg makes a SQL summary.
        if (string.IsNullOrWhiteSpace(agg))
        {
            return new GroupByStepSpec(by.Trim(), null, null);
        }

        agg = agg.Trim().ToLowerInvariant();

        if (!KnownAggregates.Contains(agg))
        {
            throw new DashSpecParseException(
                $"group_by: unknown agg '{agg}' — expected one of {string.Join("|", KnownAggregates)}.");
        }

        var needsValue = agg != "count";

        if (needsValue && string.IsNullOrWhiteSpace(value))
        {
            throw new DashSpecParseException(
                $"group_by: the 'value' parameter (the aggregate's argument column) is required for agg = {agg}.");
        }

        return new GroupByStepSpec(by.Trim(), agg, string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    /// <summary>The aggregate expression; the result column is always <c>value</c> —
    /// the grouped slot's row type declares it (e.g. <c>long value</c>).</summary>
    public string SqlAggregate =>
        Agg == "count"
            ? $"COUNT({Value ?? "*"})"
            : $"{Agg!.ToUpperInvariant()}({Value})";

    public string SelectList => $"{By} AS {By}, {SqlAggregate} AS {AggregateColumn}";

    public string GroupByClause => $"GROUP BY {By}";
}
