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

        var typedColumnsById = DashSpec.Modeling.Parse.Diagram.TableColumnsBridge.get(diagram)
            .ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

        var fields = columns
            .Select(column =>
            {
                typedColumnsById.TryGetValue(column, out var typedCol);
                var hasSchemaField = rows.Schema.FieldOrdinals.TryGetValue(column, out var ordinal);
                var schemaField = hasSchemaField ? rows.Schema.Fields[ordinal] : null;
                var optional = schemaField?.IsOptional ?? true;
                var kind = schemaField?.Kind ?? DashPrimitiveKind.String;
                var resolvedLabel = typedCol?.Label?.Value
                    ?? (labels.TryGetValue(column, out var label)
                        ? label
                        : !string.IsNullOrWhiteSpace(schemaField?.Label) ? schemaField.Label! : column);
                var resolvedFormat = typedCol?.Format?.Value
                    ?? (columnFormats.TryGetValue(column, out var format)
                        ? format
                        : schemaField?.Format);
                var resolvedAlign = typedCol?.Align is { } a ? a.Value : null;
                var resolvedHeaderAlign = typedCol?.HeaderAlign is { } ha ? ha.Value : null;
                var resolvedSize = typedCol?.Size is { } sz ? sz.Value : null;
                return new TableField(
                    column,
                    resolvedLabel,
                    kind,
                    optional,
                    resolvedFormat,
                    resolvedAlign?.WireName ?? (columnAlignments.TryGetValue(column, out var alignment) ? alignment : null),
                    resolvedHeaderAlign?.WireName ?? (headerAlignments.TryGetValue(column, out var headerAlignment) ? headerAlignment : null),
                    resolvedSize,
                    resolvedAlign,
                    resolvedHeaderAlign);
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
