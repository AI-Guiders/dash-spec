using System.Collections.Frozen;

namespace DashSpec.Abstractions.Data;

public sealed class RowTypeSchema
{
    public RowTypeSchema(string typeName, IReadOnlyList<RowFieldSchema> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        ArgumentNullException.ThrowIfNull(fields);
        if (fields.Count == 0)
        {
            throw new ArgumentException("Row type must declare at least one field.", nameof(fields));
        }

        TypeName = typeName;
        Fields = fields;
        var ordinals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < fields.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(fields[i].Name))
            {
                throw new ArgumentException($"Field at index {i} has no name.", nameof(fields));
            }

            ordinals.TryAdd(fields[i].Name, i);
        }

        FieldOrdinals = ordinals.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public string TypeName { get; }

    public IReadOnlyList<RowFieldSchema> Fields { get; }

    public FrozenDictionary<string, int> FieldOrdinals { get; }

    public int FieldCount => Fields.Count;
}
