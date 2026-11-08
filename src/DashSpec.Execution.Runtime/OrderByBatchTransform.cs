using DashSpec.Abstractions.Data;
using DashSpec.Modeling.Core;

namespace DashSpec.Execution.Runtime;

/// <summary>The <c>sort</c> display transform (ADR-0126): a stable sort of the
/// arrived rows over a DashValue column — the same wire, a different order (payload→payload,
/// the to_zone pattern). Sort keys are the values' CLR forms; nulls sort first.</summary>
public static class OrderByBatchTransform
{
    public static TypedRowBatch Apply(
        TypedRowBatch batch,
        Modeling.Parse.DataFlow.DashflowTransformParam[] parameters)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var fieldName = ResolveColumn(parameters, batch);
        var descending = ResolveDirection(parameters);

        var ordered = batch.Rows
            .Select((row, index) => (row, index))
            .OrderBy(pair => SortKey(pair.row.Get(fieldName)), StringComparer.Ordinal)
            .ThenBy(pair => pair.index)
            .Select(pair => CopyCells(pair.row))
            .ToList();
        if (descending)
        {
            ordered.Reverse();
        }

        return TypedRowBatch.Create(batch.Schema, ordered);
    }

    internal static string SortKey(DashValue value) =>
        value.IsNull ? string.Empty : (value.ToClr()?.ToString() ?? string.Empty);

    private static string ResolveColumn(
        Modeling.Parse.DataFlow.DashflowTransformParam[] parameters,
        TypedRowBatch batch)
    {
        var column = parameters.FirstOrDefault(p =>
                string.Equals(p.Key, "by", StringComparison.OrdinalIgnoreCase))?.Value
            ?? throw new DashSpecParseException(
                "sort: the 'by' parameter is required (the sort column).");

        var fieldName = column.Trim();
        for (var i = 0; i < batch.Schema.Fields.Count; i++)
        {
            if (string.Equals(batch.Schema.Fields[i].Name, fieldName, StringComparison.OrdinalIgnoreCase))
            {
                return fieldName;
            }
        }

        throw new DashSpecParseException(
            $"sort: unknown column '{column}' — not in the row schema.");
    }

    private static bool ResolveDirection(Modeling.Parse.DataFlow.DashflowTransformParam[] parameters)
    {
        var direction = parameters.FirstOrDefault(p =>
            string.Equals(p.Key, "direction", StringComparison.OrdinalIgnoreCase))?.Value;
        return string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
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
