using DashSpec.Abstractions.Data;
using DashSpec.Abstractions.Query;

namespace DashSpec.Abstractions.Connectors;

/// <summary>Read-only data access for dashboard cards and filter option lists.</summary>
public interface IDataSourceConnector
{
    string Id { get; }

    Task<RowBatch> QueryAsync(
        CompiledQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> QueryDistinctStringsAsync(string sql, CancellationToken cancellationToken = default);
}
