using DashSpec.Abstractions.Data;
using DashSpec.Abstractions.Data.Acquisition;

namespace DashSpec.Core.Tests;

internal static class TypedRowBatchTestKit
{
    public static TypedRowBatch FromDictionaries(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        if (rows.Count == 0)
        {
            return TypedRowBatch.Empty;
        }

        var columnNames = rows[0].Keys.ToList();
        var fields = columnNames
            .Select(name => new RowFieldSchema(name, InferKind(rows, name)))
            .ToList();
        var schema = new RowTypeSchema("TestRow", fields);
        var values = rows.Select(r =>
        {
            var cells = new DashValue[fields.Count];
            for (var i = 0; i < fields.Count; i++)
            {
                var field = fields[i];
                if (!r.TryGetValue(field.Name, out var raw) || raw is null)
                {
                    cells[i] = DashValue.NullOf(field.Kind);
                    continue;
                }

                cells[i] = ToDashValue(raw, field.Kind);
            }

            return cells;
        }).ToList();

        return TypedRowBatch.Create(schema, values);
    }

    private static DashPrimitiveKind InferKind(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, string name)
    {
        foreach (var row in rows)
        {
            if (!row.TryGetValue(name, out var value) || value is null)
            {
                continue;
            }

            return SqlRowMaterializer.MapFieldType(value.GetType());
        }

        return DashPrimitiveKind.String;
    }

    private static DashValue ToDashValue(object raw, DashPrimitiveKind kind) =>
        kind switch
        {
            DashPrimitiveKind.Bool => DashValue.FromBool(Convert.ToBoolean(raw)),
            DashPrimitiveKind.Int => DashValue.FromInt(Convert.ToInt32(raw)),
            DashPrimitiveKind.Decimal => DashValue.FromDecimal(Convert.ToDecimal(raw)),
            DashPrimitiveKind.String => DashValue.FromString(Convert.ToString(raw) ?? string.Empty),
            DashPrimitiveKind.Duration => DashValue.FromDuration((TimeSpan)raw),
            DashPrimitiveKind.Date => DashValue.FromDate(raw is DateOnly d ? d : DateOnly.FromDateTime((DateTime)raw)),
            DashPrimitiveKind.Time => DashValue.FromTime(raw is TimeOnly t ? t : TimeOnly.FromDateTime((DateTime)raw)),
            DashPrimitiveKind.DateTime => DashValue.FromDateTimeUtc(raw is DateTime dt ? dt : DateTime.Parse(Convert.ToString(raw)!)),
            _ => DashValue.FromString(Convert.ToString(raw) ?? string.Empty),
        };
}
