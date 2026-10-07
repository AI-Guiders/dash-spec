using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Model;
using DashSpec.Core.Platform;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Compilation;
using Microsoft.Extensions.Logging;

namespace DashSpec.Execution.Bootstrap;

/// <summary>Loads distinct field filter options via connector SQL (ADR-0099 platform).</summary>
public sealed class ReportFieldOptionsLoader(
    IReportFieldOptionsCache fieldOptionsCache,
    ILogger<ReportFieldOptionsLoader> logger)
{
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        string runtimeConfigPath,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        var fieldFilters = document.Filters.Where(x => x.Kind is FilterKind.Field).ToList();
        if (fieldFilters.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var tasks = fieldFilters.Select(filter =>
            LoadOneAsync(filter, connector, runtimeConfigPath, timeout, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.ToDictionary(x => x.Name, x => x.Values, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<(string Name, IReadOnlyList<string> Values)> LoadOneAsync(
        FilterDefinition filter,
        IDataSourceConnector connector,
        string runtimeKey,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        if (!QueryCompiler.CanLoadDistinctFieldOptions(filter))
        {
            logger.LogDebug(
                "Skipping distinct field options for {FilterName} (column '{Column}' is not table-qualified).",
                filter.Name,
                filter.ColumnReference ?? "");
            return (filter.Name, []);
        }

        var sql = QueryCompiler.BuildDistinctFieldSql(filter);
        var cacheKey = $"{runtimeKey}:{connector.Id}:{sql}";
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            var values = await fieldOptionsCache
                .GetOrLoadAsync(
                    cacheKey,
                    token => connector.QueryDistinctStringsAsync(sql, token),
                    cts.Token)
                .ConfigureAwait(false);
            sw.Stop();
            logger.LogInformation(
                "Loaded {Count} field options for filter {FilterName} in {ElapsedMs}ms",
                values.Count,
                filter.Name,
                sw.ElapsedMilliseconds);
            return (filter.Name, values);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            logger.LogWarning(
                "Timed out loading field options for filter {FilterName} after {TimeoutSeconds}s",
                filter.Name,
                timeout.TotalSeconds);
            return (filter.Name, []);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogWarning(ex, "Failed to load field options for filter {FilterName}", filter.Name);
            return (filter.Name, []);
        }
    }
}
