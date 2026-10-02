namespace DashSpec.Abstractions.Data;

public readonly struct TypedDataRow
{
    private readonly DashValue[] _cells;
    private readonly RowTypeSchema _schema;

    internal TypedDataRow(DashValue[] cells, RowTypeSchema schema)
    {
        _cells = cells;
        _schema = schema;
    }

    public RowTypeSchema Schema => _schema;

    public DashValue Get(string fieldName)
    {
        if (!_schema.FieldOrdinals.TryGetValue(fieldName, out var index))
        {
            return DashValue.NullOf(DashPrimitiveKind.String);
        }

        return (uint)index < (uint)_cells.Length ? _cells[index] : DashValue.NullOf(_schema.Fields[index].Kind);
    }

    public object? GetClr(string fieldName) => Get(fieldName).ToClr();

    public int GetInt32(string fieldName) => Get(fieldName).AsInt32();

    public double? GetDoubleOrNull(string fieldName)
    {
        var value = Get(fieldName);
        if (value.IsNull)
        {
            return null;
        }

        return value.Kind switch
        {
            DashPrimitiveKind.Int => value.AsInt32(),
            DashPrimitiveKind.Decimal => (double)value.AsDecimal(),
            DashPrimitiveKind.Bool => value.AsBool() ? 1d : 0d,
            _ => double.TryParse(value.ToClr()?.ToString(), out var parsed) ? parsed : null,
        };
    }
}
