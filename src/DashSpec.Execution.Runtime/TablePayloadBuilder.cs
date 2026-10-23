using DashSpec.Abstractions.Data;

namespace DashSpec.Execution.Runtime;

internal static class TablePayloadBuilder
{
    /// <summary>Typed table dataset (ADR-0079 §3): fields carry the wire kind from the row
    /// schema plus the spec's present intent; cells stay DashValue — display formatting is
    /// the present layer's job (LabelFormat + the report defaults on the render result).</summary>
    public static TablePayload Build(
        TypedRowBatch rows,
        DiagramDefinition diagram)
    {
        var columns = diagram.Properties.TryGetValue("columns", out var raw)
            ? raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            : rows.IsEmpty ? [] : rows.ColumnNames.ToArray();
        var columnFormats = ColumnFormatMap.Parse(
            diagram.Properties.GetValueOrDefault("column_formats"));
        var labels = ColumnLabelMap.Parse(diagram.Properties.GetValueOrDefault("column_labels"));
        var columnAlignments = ColumnFormatMap.Parse(
            diagram.Properties.GetValueOrDefault("column_alignments"));
        var headerAlignments = ColumnFormatMap.Parse(
            diagram.Properties.GetValueOrDefault("column_header_alignments"));

        var fields = columns
            .Select(column =>
            {
                var optional = rows.Schema.FieldOrdinals.TryGetValue(column, out var ordinal)
                    ? rows.Schema.Fields[ordinal].IsOptional
                    : true;
                var kind = rows.Schema.FieldOrdinals.TryGetValue(column, out ordinal)
                    ? rows.Schema.Fields[ordinal].Kind
                    : DashPrimitiveKind.String;
                return new TableField(
                    column,
                    labels.TryGetValue(column, out var label) ? label : column,
                    kind,
                    optional,
                    columnFormats.TryGetValue(column, out var format) ? format : null,
                    columnAlignments.TryGetValue(column, out var alignment) ? alignment : null,
                    headerAlignments.TryGetValue(column, out var headerAlignment) ? headerAlignment : null);
            })
            .ToList();

        var tableRows = rows.Rows
            .Select(row => columns
                .Select(column => row.Get(column))
                .ToArray())
            .ToList();

        return new TablePayload(fields, tableRows);
    }
}
