using DashSpec.Modeling.Core;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// ADR-0126: an <c>order { by, direction }</c> flow step — the database query order
/// (SQL ORDER BY, head of the chain; the layering invariant guarantees the position).
/// Display sorting is a different operation with a different name: the runtime
/// <c>sort</c> transform (or the diagram's <c>sort</c> chrome key) — the BI layer split.
/// </summary>
public sealed record OrderByStepSpec(string By, string Direction)
{
    public const string PluginIdName = "order";

    public static OrderByStepSpec? Find(DashflowExecutionPath? path)
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
                    return Parse(step.Parameters);
                }
            }
        }

        return null;
    }

    /// <summary>Loud parameter contract: <c>by</c> required; <c>direction</c> one of
    /// asc|desc (default asc).</summary>
    public static OrderByStepSpec Parse(Modeling.Parse.DataFlow.DashflowTransformParam[] parameters)
    {
        string? by = null, direction = null;

        foreach (var parameter in parameters)
        {
            if (string.Equals(parameter.Key, "by", StringComparison.OrdinalIgnoreCase))
            {
                by = parameter.Value;
            }
            else if (string.Equals(parameter.Key, "direction", StringComparison.OrdinalIgnoreCase))
            {
                direction = parameter.Value;
            }
        }

        if (string.IsNullOrWhiteSpace(by))
        {
            throw new DashSpecParseException(
                "order: the 'by' parameter is required (the sort column).");
        }

        direction = string.IsNullOrWhiteSpace(direction) ? "asc" : direction.Trim().ToLowerInvariant();

        if (direction is not ("asc" or "desc"))
        {
            throw new DashSpecParseException(
                $"order: unknown direction '{direction}' — expected asc or desc.");
        }

        return new OrderByStepSpec(by.Trim(), direction);
    }


    /// <summary>The SQL tail for the query (e.g. <c>program DESC</c>).</summary>
    public string SqlOrderBy => Direction == "desc" ? $"{By} DESC" : $"{By} ASC";
}
