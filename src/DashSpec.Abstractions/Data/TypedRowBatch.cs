namespace DashSpec.Abstractions.Data;

/// <summary><c>rows R</c> wire snapshot (ADR-0087).</summary>
public sealed class TypedRowBatch
{
    public static TypedRowBatch Empty { get; } = new(
        new RowTypeSchema("_empty", [new RowFieldSchema("_", DashPrimitiveKind.String)]),
        []);

    private TypedRowBatch(RowTypeSchema schema, IReadOnlyList<TypedDataRow> rows)
    {
        Schema = schema;
        Rows = rows;
    }

    public RowTypeSchema Schema { get; }

    public IReadOnlyList<TypedDataRow> Rows { get; }

    public int Count => Rows.Count;

    public bool IsEmpty => Rows.Count == 0;

    public TypedDataRow this[int index] => Rows[index];

    public IReadOnlyList<string> ColumnNames =>
        Schema.Fields.Select(f => f.Name).ToArray();

    public static TypedRowBatch Create(RowTypeSchema schema, IReadOnlyList<DashValue[]> rowValues)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(rowValues);

        var rows = new TypedDataRow[rowValues.Count];
        for (var i = 0; i < rowValues.Count; i++)
        {
            var cells = rowValues[i] ?? throw new ArgumentException($"Row {i} is null.", nameof(rowValues));
            if (cells.Length != schema.FieldCount)
            {
                throw new ArgumentException(
                    $"Row {i} has {cells.Length} cells but schema declares {schema.FieldCount} fields.",
                    nameof(rowValues));
            }

            rows[i] = new TypedDataRow(cells, schema);
        }

        return new TypedRowBatch(schema, rows);
    }
}
