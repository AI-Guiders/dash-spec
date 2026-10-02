using DashSpec.Abstractions.Data;

namespace DashSpec.Abstractions.Query;

public sealed record CompiledQuery(
    string Sql,
    IReadOnlyList<QueryParameter> Parameters,
    RowTypeSchema RowSchema);

public sealed record QueryParameter(string Name, object Value);
