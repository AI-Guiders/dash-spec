using DashSpec.Abstractions.Query;

namespace DashSpec.Abstractions.Connectors;

/// <summary>Read-only data access for dashboard cards and filter option lists.</summary>
public interface IDataSourceConnector
{
    string Id { get; }

    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(
        CompiledQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> QueryDistinctStringsAsync(string sql, CancellationToken cancellationToken = default);
}
