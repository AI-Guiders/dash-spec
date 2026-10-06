using DashSpec.Abstractions.Data;
using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

/// <summary>Query compile source + optional dashflow transforms for a card (ADR-0078 P3).</summary>
public static class DashflowCardDataPipeline
{
    public static TypedRowBatch ApplyTransforms(DashboardDocument document, CardDefinition card, TypedRowBatch rows) =>
        ApplyTransforms(document, card.FlowInput, rows);

    public static TypedRowBatch ApplyTransforms(
        DashboardDocument document,
        CardFlowInputDefinition? flowInput,
        TypedRowBatch rows)
    {
        if (flowInput is null || document.Dashflow is null || rows.IsEmpty)
        {
            return rows;
        }

        var path = DashflowPathResolver.Resolve(document.Dashflow, flowInput);
        return path.Transformers.Count == 0
            ? rows
            : DashflowBatchExecutor.Apply(rows, path.Transformers);
    }
}
