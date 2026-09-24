using DashSpec.Core.Model;

namespace DashSpec.Execution.Runtime;

internal static class TablePayloadBuilder
{
    public static TablePayload Build(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        DiagramDefinition diagram)
    {
        var columns = diagram.Properties.TryGetValue("columns", out var raw)
            ? raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : rows.FirstOrDefault()?.Keys.ToArray() ?? [];
        var columnFormats = ColumnFormatMap.Parse(
            diagram.Properties.GetValueOrDefault("column_formats"));

        var tableRows = rows
            .Select(row => columns
                .Select(column =>
                {
                    var value = row.GetValueOrDefault(column);
                    return columnFormats.TryGetValue(column, out var format)
                        ? LabelFormat.FormatObject(value, format)
                        : PayloadRowFormatters.FormatValue(value);
                })
                .ToList())
            .ToList();

        return new TablePayload(columns.ToList(), tableRows);
    }
}
