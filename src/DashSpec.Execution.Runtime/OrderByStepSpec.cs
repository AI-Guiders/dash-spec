using DashSpec.Modeling.Core;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// ADR-0123: an <c>order_by { by, direction }</c> flow step. The carrier is positional, not
/// the step's own: the compiler pushes it into the SQL <c>ORDER BY</c> only when every node
/// of the path before it is database (ORDER BY does not commute with collapsing nodes —
/// TOP/reduce — pushing it through a runtime node would change which rows arrive at all).
/// Otherwise the step stays runtime and sorts what arrived (stable sort over DashValue).
/// </summary>
public sealed record OrderByStepSpec(string By, string Direction)
{
    public const string PluginIdName = "order_by";

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
                "order_by: the 'by' parameter is required (the sort column).");
        }

        direction = string.IsNullOrWhiteSpace(direction) ? "asc" : direction.Trim().ToLowerInvariant();

        if (direction is not ("asc" or "desc"))
        {
            throw new DashSpecParseException(
                $"order_by: unknown direction '{direction}' — expected asc or desc.");
        }

        return new OrderByStepSpec(by.Trim(), direction);
    }

    /// <summary>The step may ride the query only when nothing runtime sits before it on the
    /// path — the positional pushdown rule (ADR-0123 §5).</summary>
    public static bool CanPushDown(DashflowExecutionPath? path)
    {
        if (path is null)
        {
            return false;
        }

        // Runtime steps currently known to the executor; anything unknown stays on the
        // safe side (runtime) — the dictionary grows through ADRs only.
        var runtimeSteps = new[]
        {
            BuiltinScalarTransformIds.ToZone,
            BuiltinScalarTransformIds.IanaToTimeShift,
            BuiltinScalarTransformIds.TimeShiftToIana,
            BuiltinScalarTransformIds.Reduce,
        };

        foreach (var transformer in path.Transformers)
        {
            foreach (var step in transformer.Steps)
            {
                if (runtimeSteps.Contains(step.PluginId, StringComparer.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>The SQL tail for the query (e.g. <c>program DESC</c>).</summary>
    public string SqlOrderBy => Direction == "desc" ? $"{By} DESC" : $"{By} ASC";
}
