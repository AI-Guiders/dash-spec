using DashSpec.Abstractions.Data;

namespace DashSpec.Execution.Runtime;

/// <summary>
/// Runtime <c>round</c> transform: rounds decimal columns in a <see cref="TypedRowBatch"/>
/// to <c>digits</c> decimal places (default 1). If <c>column</c> is specified, rounds only
/// that column; otherwise rounds all <see cref="DashPrimitiveKind.Decimal"/> columns.
/// </summary>
public static class RoundBatchTransform
{
    public static TypedRowBatch Apply(
        TypedRowBatch batch,
        IReadOnlyList<DashflowTransformParameterDefinition> parameters)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(parameters);

        if (batch.IsEmpty)
        {
            return batch;
        }

        var digits = 1;
        var digitsRaw = parameters.FirstOrDefault(p =>
            string.Equals(p.Key, "digits", StringComparison.OrdinalIgnoreCase))?.Value;
        if (!string.IsNullOrWhiteSpace(digitsRaw))
        {
            if (!int.TryParse(digitsRaw, out digits) || digits < 0 || digits > 28)
            {
                throw new InvalidOperationException($"round: 'digits' must be an integer between 0 and 28, got '{digitsRaw}'.");
            }
        }

        var targetColumn = parameters.FirstOrDefault(p =>
            string.Equals(p.Key, "column", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();

        int[] targetIndexes;
        if (!string.IsNullOrWhiteSpace(targetColumn))
        {
            if (!batch.Schema.FieldOrdinals.TryGetValue(targetColumn, out var idx))
            {
                throw new InvalidOperationException($"round: unknown column '{targetColumn}' — not in the row schema.");
            }

            targetIndexes = batch.Schema.Fields[idx].Kind == DashPrimitiveKind.Decimal ? [idx] : [];
        }
        else
        {
            targetIndexes = batch.Schema.Fields
                .Select((field, index) => (field, index))
                .Where(t => t.field.Kind == DashPrimitiveKind.Decimal)
                .Select(t => t.index)
                .ToArray();
        }

        if (targetIndexes.Length == 0)
        {
            return batch;
        }

        var rowValues = new List<DashValue[]>(batch.Count);
        foreach (var row in batch.Rows)
        {
            var cells = new DashValue[batch.Schema.FieldCount];
            for (var i = 0; i < batch.Schema.FieldCount; i++)
            {
                cells[i] = row.Get(batch.Schema.Fields[i].Name);
            }

            foreach (var index in targetIndexes)
            {
                var cell = cells[index];
                if (!cell.IsNull && cell.Kind == DashPrimitiveKind.Decimal)
                {
                    cells[index] = DashValue.FromDecimal(
                        Math.Round(cell.AsDecimal(), digits, MidpointRounding.AwayFromZero));
                }
            }

            rowValues.Add(cells);
        }

        var created = TypedRowBatch.Create(batch.Schema, rowValues);
        return batch.Groups is not null && !string.IsNullOrWhiteSpace(batch.GroupFieldName)
            ? created.WithGroups(batch.GroupFieldName, batch.Groups)
            : created;
    }
}
