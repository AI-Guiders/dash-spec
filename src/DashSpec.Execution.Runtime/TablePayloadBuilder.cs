using DashSpec.Abstractions.Data;

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

        var labels = ColumnLabelMap.Parse(diagram.Properties.GetValueOrDefault("column_labels"));
        var headerTexts = labels.Count > 0
            ? columns.Select(column => labels.TryGetValue(column, out var label) ? label : column).ToList()
            : null;

        return new TablePayload(columns.ToList(), tableRows, headerTexts);
    }
}
