using DashSpec.Abstractions.Data;

namespace DashSpec.Abstractions.Query;

public sealed record CompiledQuery(
    string Sql,
    IReadOnlyList<QueryParameter> Parameters,
    RowTypeSchema RowSchema,
    /// <summary>Хранение времени в источнике: "utc" | "local"; null — не заявлено (по умолчанию UTC).</summary>
    string? DateTimeFrame = null);

public sealed record QueryParameter(string Name, object Value);
