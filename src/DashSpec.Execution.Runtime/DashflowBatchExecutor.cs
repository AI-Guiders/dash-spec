using DashSpec.Abstractions.Data;
using DashSpec.Execution.Runtime;

namespace DashSpec.Execution.Runtime;

/// <summary>Run dashflow transformer steps on a typed row batch (ADR-0078 / ADR-0080 v1).</summary>
public static class DashflowBatchExecutor
{
    public static TypedRowBatch Apply(TypedRowBatch batch, IReadOnlyList<DashflowTransformerDefinition> transformers)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(transformers);

        var current = batch;
        foreach (var transformer in transformers)
        {
            foreach (var step in transformer.Steps)
            {
                current = ApplyStep(current, step, transformer.Id);
            }
        }

        return current;
    }

    private static TypedRowBatch ApplyStep(
        TypedRowBatch batch,
        DashflowTransformStepDefinition step,
        string transformerId)
    {
        if (step.PluginId.Equals(BuiltinScalarTransformIds.ToZone, StringComparison.OrdinalIgnoreCase))
        {
            return ToZoneBatchTransform.Apply(batch, step.Parameters);
        }

        if (step.PluginId.Equals(BuiltinScalarTransformIds.IanaToTimeShift, StringComparison.OrdinalIgnoreCase)
            || step.PluginId.Equals(BuiltinScalarTransformIds.TimeShiftToIana, StringComparison.OrdinalIgnoreCase))
        {
            return batch;
        }

        if (step.PluginId.Equals(BuiltinScalarTransformIds.Reduce, StringComparison.OrdinalIgnoreCase))
        {
            return ReduceBatchTransform.Apply(batch, step.Parameters);
        }

        if (step.PluginId.Equals(OrderByStepSpec.PluginIdName, StringComparison.OrdinalIgnoreCase))
        {
            // ADR-0123: the runtime face of order_by — a stable sort over what arrived
            // (the step rode the query only over a purely database path).
            return OrderByBatchTransform.Apply(batch, step.Parameters);
        }

        if (step.PluginId.Equals(GroupByStepSpec.PluginIdName, StringComparison.OrdinalIgnoreCase))
        {
            // ADR-0121 (database summary): the renderer compiled it into the slot's SQL —
            // the rows arrive already collapsed. ADR-0123 (runtime grouping, by only):
            // the batch gains contiguous group runs; rows stay rows.
            var hasAgg = Array.Exists(step.Parameters, p =>
                string.Equals(p.Key, "agg", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(p.Value));
            return hasAgg ? batch : GroupByBatchTransform.Apply(batch, step.Parameters);
        }

        throw new NotSupportedException(
            $"Dashflow transform '{step.PluginId}' on transformer '{transformerId}' is not supported at runtime yet.");
    }
}
