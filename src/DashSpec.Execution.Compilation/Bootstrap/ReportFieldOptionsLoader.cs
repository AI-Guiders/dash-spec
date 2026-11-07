using DashSpec.Abstractions.Connectors;
using DashSpec.Execution.Runtime;
using DashSpec.Execution.Compilation;
using Microsoft.Extensions.Logging;

using DashSpec.Compiler.Contract;

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
        TimeSpan timeout,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? currentSelections = null)
    {
        var fieldFilters = document.Filters.Where(x => x.Kind is FilterKind.Field).ToList();
        if (fieldFilters.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var definitions = document.Filters.ToDictionary(x => x.Name, x => x, StringComparer.OrdinalIgnoreCase);

        var tasks = fieldFilters.Select(filter =>
            LoadOneAsync(filter, definitions, currentSelections, connector, runtimeConfigPath, timeout, cancellationToken));
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results.ToDictionary(x => x.Name, x => x.Values, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<(string Name, IReadOnlyList<string> Values)> LoadOneAsync(
        FilterDefinition filter,
        IReadOnlyDictionary<string, FilterDefinition> definitions,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? currentSelections,
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

        // ADR-0120 cascade domains: the option domain of a `depends` filter is
        // constrained by the depended filter's current selection (field → equality).
        // Bootstrap (no selections yet) keeps the full domain; reload after a value
        // change narrows it. The predicate rides the SQL, so the cache key
        // distinguishes domains automatically.
        var sql =
            filter.DependsOn is { } dependsOn && !string.IsNullOrWhiteSpace(dependsOn.Value)
                ? QueryCompiler.BuildDistinctFieldSql(filter, definitions, ResolveSelection(definitions, currentSelections, dependsOn.Value))
                : QueryCompiler.BuildDistinctFieldSql(filter);
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

    /// <summary>Selection of the depended filter: `report.&lt;id&gt;` strips the prefix,
    /// a bare id is the sibling name. No selection yet (bootstrap) → null → full domain.</summary>
    private static IReadOnlyList<string>? ResolveSelection(
        IReadOnlyDictionary<string, FilterDefinition> definitions,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? currentSelections,
        string dependsOn)
    {
        var targetName = dependsOn.StartsWith("report.", StringComparison.OrdinalIgnoreCase)
            ? dependsOn["report.".Length..]
            : dependsOn;

        if (currentSelections is null || !currentSelections.TryGetValue(targetName, out var selection))
        {
            return null;
        }

        return selection.Count == 0 ? null : selection;
    }
}
