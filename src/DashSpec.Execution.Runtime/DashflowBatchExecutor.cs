using DashSpec.Abstractions.Data;
using DashSpec.Core.Model;
using DashSpec.Core.Transforms;

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

        throw new NotSupportedException(
            $"Dashflow transform '{step.PluginId}' on transformer '{transformerId}' is not supported at runtime yet.");
    }
}
