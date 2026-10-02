namespace DashSpec.Abstractions.Data;

public sealed record RowFieldSchema(string Name, DashPrimitiveKind Kind, bool IsOptional = false);
