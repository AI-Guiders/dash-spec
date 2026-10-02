using System.Collections.Frozen;

namespace DashSpec.Abstractions.Data;

/// <summary>Typed row snapshot at the acquisition → flow boundary (ADR-0085 A1).</summary>
public sealed class RowBatch
{
    public static RowBatch Empty { get; } = new([], FrozenDictionary<string, int>.Empty, []);

    private readonly FrozenDictionary<string, int> _columnOrdinals;

    private RowBatch(
        IReadOnlyList<string> columnNames,
        FrozenDictionary<string, int> columnOrdinals,
        IReadOnlyList<DataRow> rows)
    {
        ColumnNames = columnNames;
        _columnOrdinals = columnOrdinals;
        Rows = rows;
    }

    public IReadOnlyList<string> ColumnNames { get; }

    public IReadOnlyList<DataRow> Rows { get; }

    public int Count => Rows.Count;

    public bool IsEmpty => Rows.Count == 0;

    public DataRow this[int index] => Rows[index];

    public static RowBatch Create(IReadOnlyList<string> columnNames, IReadOnlyList<object?[]> rowValues)
    {
        ArgumentNullException.ThrowIfNull(columnNames);
        ArgumentNullException.ThrowIfNull(rowValues);

        var ordinals = BuildOrdinals(columnNames);
        var rows = new DataRow[rowValues.Count];
        for (var i = 0; i < rowValues.Count; i++)
        {
            var cells = rowValues[i] ?? throw new ArgumentException($"Row {i} is null.", nameof(rowValues));
            if (cells.Length != columnNames.Count)
            {
                throw new ArgumentException(
                    $"Row {i} has {cells.Length} cells but {columnNames.Count} columns were declared.",
                    nameof(rowValues));
            }

            rows[i] = new DataRow(cells, ordinals);
        }

        return new RowBatch(columnNames, ordinals, rows);
    }

    private static FrozenDictionary<string, int> BuildOrdinals(IReadOnlyList<string> columnNames)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < columnNames.Count; i++)
        {
            var name = columnNames[i];
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Column names must be non-empty.", nameof(columnNames));
            }

            map.TryAdd(name, i);
        }

        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>One row in a <see cref="RowBatch"/>; column lookup is ordinal-backed.</summary>
public readonly struct DataRow
{
    private readonly object?[] _cells;
    private readonly FrozenDictionary<string, int> _columnOrdinals;

    internal DataRow(object?[] cells, FrozenDictionary<string, int> columnOrdinals)
    {
        _cells = cells;
        _columnOrdinals = columnOrdinals;
    }

    public object? GetValueOrDefault(string column) =>
        _columnOrdinals.TryGetValue(column, out var index) && (uint)index < (uint)_cells.Length
            ? _cells[index]
            : null;

    public bool TryGetValue(string column, out object? value)
    {
        if (_columnOrdinals.TryGetValue(column, out var index) && (uint)index < (uint)_cells.Length)
        {
            value = _cells[index];
            return true;
        }

        value = null;
        return false;
    }
}
