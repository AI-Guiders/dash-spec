namespace DashSpec.Abstractions.Query;

public sealed record CompiledQuery(string Sql, IReadOnlyList<QueryParameter> Parameters);

public sealed record QueryParameter(string Name, object Value);
