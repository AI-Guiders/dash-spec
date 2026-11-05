using System.Data.Common;

namespace DashSpec.Abstractions.Data.Acquisition;

/// <summary>Maps ADO readers to <see cref="TypedRowBatch"/> using Modeling-provided <see cref="RowTypeSchema"/> (DSPEC031–034).</summary>
public static class SqlRowMaterializer
{
    public static int[] ResolveColumnOrdinals(DbDataReader reader, RowTypeSchema schema)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(schema);

        var available = ReadColumnNames(reader);
        var ordinals = new int[schema.FieldCount];
        for (var i = 0; i < schema.FieldCount; i++)
        {
            var fieldName = schema.Fields[i].Name;
            try
            {
                ordinals[i] = reader.GetOrdinal(fieldName);
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
            {
                if (schema.Fields[i].IsOptional)
                {
                    ordinals[i] = -1;
                    continue;
                }

                throw new InvalidOperationException(
                    FormatMissingColumn(schema.TypeName, fieldName, available),
                    ex);
            }
        }

        return ordinals;
    }

    private static string FormatMissingColumn(
        string rowTypeName,
        string fieldName,
        IReadOnlyList<string> availableColumns)
    {
        var available =
            availableColumns.Count == 0
                ? "(no columns returned)"
                : string.Join(", ", availableColumns);
        return
            $"SQL result is missing column '{fieldName}' required by row type '{rowTypeName}'. " +
            $"Columns returned: {available}. " +
            "Align the dashflow row type with the SQL view, or add the column to the view/query.";
    }

    private static List<string> ReadColumnNames(DbDataReader reader)
    {
        var names = new List<string>(reader.FieldCount);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            names.Add(reader.GetName(i));
        }

        return names;
    }

    public static DashValue[] ReadRow(DbDataReader reader, RowTypeSchema schema, int[] columnOrdinals, string? dateTimeFrame = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(columnOrdinals);

        if (columnOrdinals.Length != schema.FieldCount)
        {
            throw new ArgumentException("Column ordinal map length must match schema field count.", nameof(columnOrdinals));
        }

        var cells = new DashValue[schema.FieldCount];
        for (var i = 0; i < schema.FieldCount; i++)
        {
            var field = schema.Fields[i];
            var ordinal = columnOrdinals[i];
            if (ordinal < 0 || reader.IsDBNull(ordinal))
            {
                cells[i] = DashValue.NullOf(field.Kind);
                continue;
            }

            cells[i] = ConvertCell(reader.GetValue(ordinal), field.Kind, dateTimeFrame);
        }

        return cells;
    }

    public static TypedRowBatch ReadAll(DbDataReader reader, int maxRows, RowTypeSchema schema, string? dateTimeFrame = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(schema);

        var columnOrdinals = ResolveColumnOrdinals(reader, schema);
        var rows = new List<DashValue[]>();
        while (reader.Read())
        {
            if (rows.Count >= maxRows)
            {
                throw new InvalidOperationException($"SQL result exceeded max_rows ({maxRows}).");
            }

            rows.Add(ReadRow(reader, schema, columnOrdinals, dateTimeFrame));
        }

        return TypedRowBatch.Create(schema, rows);
    }

    public static DashPrimitiveKind MapClrType(Type fieldType)
    {
        var underlying = Nullable.GetUnderlyingType(fieldType) ?? fieldType;
        return underlying switch
        {
            _ when underlying == typeof(bool) => DashPrimitiveKind.Bool,
            _ when underlying == typeof(byte) || underlying == typeof(sbyte) || underlying == typeof(short)
                || underlying == typeof(ushort) || underlying == typeof(int) || underlying == typeof(uint)
                || underlying == typeof(long) || underlying == typeof(ulong) => DashPrimitiveKind.Int,
            _ when underlying == typeof(decimal) || underlying == typeof(float) || underlying == typeof(double)
                => DashPrimitiveKind.Decimal,
            _ when underlying == typeof(string) || underlying == typeof(char) || underlying == typeof(Guid)
                => DashPrimitiveKind.String,
            _ when underlying == typeof(TimeSpan) => DashPrimitiveKind.Duration,
            _ when underlying == typeof(DateOnly) => DashPrimitiveKind.Date,
            _ when underlying == typeof(TimeOnly) => DashPrimitiveKind.Time,
            _ when underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) => DashPrimitiveKind.DateTime,
            _ => DashPrimitiveKind.String,
        };
    }

    private static DashValue ConvertCell(object raw, DashPrimitiveKind kind, string? dateTimeFrame)
    {
        return kind switch
        {
            DashPrimitiveKind.Bool => DashValue.FromBool(Convert.ToBoolean(raw)),
            DashPrimitiveKind.Int => DashValue.FromInt(Convert.ToInt32(raw)),
            DashPrimitiveKind.Decimal => DashValue.FromDecimal(Convert.ToDecimal(raw)),
            DashPrimitiveKind.String => DashValue.FromString(Convert.ToString(raw) ?? string.Empty),
            DashPrimitiveKind.Duration => DashValue.FromDuration(raw switch
            {
                TimeSpan ts => ts,
                _ => TimeSpan.FromTicks(Convert.ToInt64(raw)),
            }),
            DashPrimitiveKind.Date => DashValue.FromDate(raw switch
            {
                DateOnly d => d,
                DateTime dt => DateOnly.FromDateTime(dt),
                _ => DateOnly.Parse(Convert.ToString(raw)!),
            }),
            DashPrimitiveKind.Time => DashValue.FromTime(raw switch
            {
                TimeOnly t => t,
                DateTime dt => TimeOnly.FromDateTime(dt),
                TimeSpan ts => TimeOnly.FromTimeSpan(ts),
                _ => TimeOnly.Parse(Convert.ToString(raw)!),
            }),
            // Storage contract: *_utc columns hold UTC. datetime2 arrives Kind=Unspecified — the source's
            // time_zone declaration decides: "local" converts; "utc" (or undeclared) keeps the wall value as UTC.
            // ToUniversalTime() on Unspecified would treat it as server-local and silently shift it (MSK host: −3h).
            DashPrimitiveKind.DateTime => DashValue.FromDateTimeUtc(NormalizeStoredDateTime(raw, dateTimeFrame)),
            _ => DashValue.FromString(Convert.ToString(raw) ?? string.Empty),
        };
    }

    private static DateTime NormalizeStoredDateTime(object raw, string? dateTimeFrame)
    {
        var dt = raw switch
        {
            DateTime dtValue => dtValue,
            DateTimeOffset dto => dto.UtcDateTime,
            _ => DateTime.Parse(Convert.ToString(raw)!, System.Globalization.CultureInfo.InvariantCulture),
        };

        if (dt.Kind == DateTimeKind.Utc)
        {
            return dt;
        }

        return dateTimeFrame == "local"
            ? dt.ToUniversalTime()
            : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }
}
