using DashSpec.Abstractions.Data;
using DashSpec.Core.Model;

namespace DashSpec.Core.Transforms;

/// <summary>Apply <c>to_zone</c> on typed row batches (UTC DateTime cells → fixed-offset civil).</summary>
public static class ToZoneBatchTransform
{
    public static TypedRowBatch Apply(TypedRowBatch batch, IReadOnlyList<DashflowTransformParameterDefinition> parameters)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(parameters);

        if (!TryGetOffsetMinutes(parameters, out var offsetMinutes))
        {
            throw new InvalidOperationException("to_zone step is missing offset_minutes (re-parse dashflow or normalize at compile time).");
        }

        if (batch.IsEmpty)
        {
            return batch;
        }

        var dateTimeFieldIndexes = batch.Schema.Fields
            .Select((field, index) => (field, index))
            .Where(t => t.field.Kind == DashPrimitiveKind.DateTime)
            .Select(t => t.index)
            .ToArray();

        if (dateTimeFieldIndexes.Length == 0)
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

            foreach (var index in dateTimeFieldIndexes)
            {
                var cell = cells[index];
                if (cell.IsNull || cell.Kind != DashPrimitiveKind.DateTime)
                {
                    continue;
                }

                cells[index] = DashValue.FromDateTimeDisplayWallClock(
                    ConvertUtcToOffset(cell.AsDateTimeUtc(), offsetMinutes));
            }

            rowValues.Add(cells);
        }

        return TypedRowBatch.Create(batch.Schema, rowValues);
    }

    internal static DateTime ConvertUtcToOffset(DateTime utc, int offsetMinutes)
    {
        var normalized = utc.Kind == DateTimeKind.Utc
            ? utc
            : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return DateTime.SpecifyKind(normalized.AddMinutes(offsetMinutes), DateTimeKind.Unspecified);
    }

    private static bool TryGetOffsetMinutes(
        IReadOnlyList<DashflowTransformParameterDefinition> parameters,
        out int offsetMinutes)
    {
        offsetMinutes = 0;
        foreach (var parameter in parameters)
        {
            if (!parameter.Key.Equals("offset_minutes", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return int.TryParse(parameter.Value, out offsetMinutes);
        }

        return false;
    }
}
