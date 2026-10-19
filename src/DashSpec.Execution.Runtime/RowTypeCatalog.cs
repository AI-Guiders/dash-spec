using System.Collections.Frozen;
using DashSpec.Abstractions.Data;

namespace DashSpec.Execution.Runtime;

/// <summary>Execution view of Modeling row types (ADR-0087 B2).</summary>
public sealed class RowTypeCatalog
{
    private readonly FrozenDictionary<string, RowTypeSchema> _schemas;

    public RowTypeCatalog(IReadOnlyDictionary<string, RowTypeSchema> schemas)
    {
        ArgumentNullException.ThrowIfNull(schemas);
        _schemas = schemas.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    public static RowTypeCatalog FromDocument(DashSpec.Modeling.Parse.Document.DashboardDocument document) =>
        new(document.ResolvedRowTypeSchemas);

    public RowTypeSchema GetRequired(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new InvalidOperationException("Card datasource rows type is not declared.");
        }

        if (!_schemas.TryGetValue(typeName, out var schema))
        {
            throw new InvalidOperationException($"Row type '{typeName}' is not defined in Modeling (declare it in a types unit and import it by namespace).");
        }

        return schema;
    }
}
