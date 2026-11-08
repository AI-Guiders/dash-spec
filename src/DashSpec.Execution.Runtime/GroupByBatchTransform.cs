using DashSpec.Abstractions.Data;
using DashSpec.Modeling.Core;

namespace DashSpec.Execution.Runtime;

/// <summary>Runtime face of the <c>group_by { by }</c> step (ADR-0123): grouping, not a
/// summary — rows stay rows. The batch is ordered by the group key (stable) and gains
/// contiguous group runs over that key; the renderer only draws the organization.</summary>
public static class GroupByBatchTransform
{
    public static TypedRowBatch Apply(
        TypedRowBatch batch,
        Modeling.Parse.DataFlow.DashflowTransformParam[] parameters)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var column = parameters.FirstOrDefault(p =>
                string.Equals(p.Key, "by", StringComparison.OrdinalIgnoreCase))?.Value
            ?? throw new DashSpecParseException(
                "group_by: the 'by' parameter is required (the grouping column).");

        var fieldName = column.Trim();
        RowFieldSchema? field = null;
        for (var i = 0; i < batch.Schema.Fields.Count; i++)
        {
            if (string.Equals(batch.Schema.Fields[i].Name, fieldName, StringComparison.OrdinalIgnoreCase))
            {
                field = batch.Schema.Fields[i];
                break;
            }
        }

        if (field is null)
        {
            throw new DashSpecParseException(
                $"group_by: unknown column '{column}' — not in the row schema.");
        }

        // ORDER BY key + один проход по результатам (ADR-0123): stable order, then runs.
        var ordered = batch.Rows
            .Select((row, index) => (row, index, key: OrderByBatchTransform.SortKey(row.Get(fieldName))))
            .OrderBy(pair => pair.key, StringComparer.Ordinal)
            .ThenBy(pair => pair.index)
            .ToList();

        var runs = new List<RowGroupRun>();
        var rows = new DashValue[ordered.Count][];
        var offset = 0;
        while (offset < ordered.Count)
        {
            var key = ordered[offset].key;
            var length = 1;
            rows[offset] = CopyCells(ordered[offset].row);
            while (offset + length < ordered.Count &&
                   string.Equals(ordered[offset + length].key, key, StringComparison.Ordinal))
            {
                rows[offset + length] = CopyCells(ordered[offset + length].row);
                length++;
            }

            runs.Add(new RowGroupRun(key, offset, length));
            offset += length;
        }

        return batch.WithGroups(field.Name, runs, rows);
    }

    private static DashValue[] CopyCells(TypedDataRow row)
    {
        var cells = new DashValue[row.Schema.FieldCount];
        for (var i = 0; i < cells.Length; i++)
        {
            cells[i] = row.Get(row.Schema.Fields[i].Name);
        }

        return cells;
    }
}
