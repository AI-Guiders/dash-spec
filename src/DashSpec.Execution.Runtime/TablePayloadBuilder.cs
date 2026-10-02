using DashSpec.Abstractions.Data;
using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

internal static class TablePayloadBuilder
{
    public static TablePayload Build(
        TypedRowBatch rows,
        DiagramDefinition diagram)
    {
        var columns = diagram.Properties.TryGetValue("columns", out var raw)
            ? raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : rows.IsEmpty ? [] : rows.ColumnNames.ToArray();
        var columnFormats = ColumnFormatMap.Parse(
            diagram.Properties.GetValueOrDefault("column_formats"));

        var tableRows = rows.Rows
            .Select(row => columns
                .Select(column =>
                {
                    var value = row.GetClr(column);
                    return columnFormats.TryGetValue(column, out var format)
                        ? LabelFormat.FormatObject(value, format)
                        : PayloadRowFormatters.FormatValue(value);
                })
                .ToList())
            .ToList();

        return new TablePayload(columns.ToList(), tableRows);
    }
}
