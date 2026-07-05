using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Compilation;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
using DashSpec.Host.Configuration;
using DashSpec.Host.Plugins;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Models;

namespace DashSpec.Host.Services.Loading;

public sealed class DashboardSpecLoader(
    ConnectorRegistry connectorRegistry,
    ConnectorPluginManifest pluginManifest,
    DashSpecHostContext hostContext,
    DashSpecParseOptionsProvider parseOptionsProvider,
    ILogger<DashboardSpecLoader> logger) : IDashboardSpecLoader
{
    public async Task<LoadedDashboard> LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel,
        CancellationToken cancellationToken = default,
        SpecLoadOptions? options = null)
    {
        options ??= new SpecLoadOptions();
        var entryRuntime = DashSpecParser.ReadRuntimePath(text);
        if (string.IsNullOrWhiteSpace(entryRuntime) ||
            !string.Equals(entryRuntime, hostContext.StartupRuntimeReference, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Этот дашборд ссылается на другой @runtime ({entryRuntime ?? "(нет)"}). " +
                $"Ожидается {hostContext.StartupRuntimeReference} — все entry catalog должны ссылаться на один @runtime TOML.");
        }

        var configPath = DashSpecBootstrap.ResolveRuntimeConfigPath(
            specFullPath,
            text,
            hostContext.DefaultSpecDirectory);
        if (!string.Equals(configPath, hostContext.StartupRuntimeConfigPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Файл @runtime разрешается в другой путь ({Path.GetFileName(configPath)}). " +
                $"Положите {hostContext.StartupRuntimeReference} рядом с .dashspec или перезапустите Host.");
        }

        var document = DashSpecParser.Parse(
            text,
            Path.GetDirectoryName(specFullPath),
            parseOptionsProvider.CreateOptions());
        var library = SpecLibraryComposer.Load(
            specFullPath,
            document.DiagramLibraryPath,
            document.PalettePath,
            hostContext.DefaultSpecDirectory,
            document);
        _ = SpecResolver.Resolve(document, library);
        var connector = connectorRegistry.Resolve(document.ConnectorId, pluginManifest.DefaultConnectorId);
        var filterIndex = DashboardBootstrap.IndexFilters(document);
        var filters = DashboardBootstrap.CreateInitialFilters(document, DateOnly.FromDateTime(DateTime.UtcNow));
        var fieldOptions = options.LoadFieldOptions
            ? await LoadFieldOptionsAsync(document, connector, cancellationToken, options.FieldOptionsTimeout)
                .ConfigureAwait(false)
            : new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        return new LoadedDashboard(
            document,
            library,
            connector,
            filterIndex,
            filters,
            fieldOptions,
            sourceLabel,
            Path.GetDirectoryName(specFullPath));
    }

    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadFieldOptionsAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null) =>
        LoadFieldOptionsCoreAsync(document, connector, cancellationToken, timeout ?? TimeSpan.FromSeconds(20));

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadFieldOptionsCoreAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        CancellationToken cancellationToken,
        TimeSpan timeout)
    {
        var fieldOptions = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var filter in document.Filters.Where(x => x.Kind is FilterKind.Field))
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeout);
                var sql = QueryCompiler.BuildDistinctFieldSql(filter);
                fieldOptions[filter.Name] = await connector
                    .QueryDistinctStringsAsync(sql, cts.Token)
                    .ConfigureAwait(false);
                sw.Stop();
                logger.LogInformation(
                    "Loaded {Count} field options for filter {FilterName} in {ElapsedMs}ms",
                    fieldOptions[filter.Name].Count,
                    filter.Name,
                    sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                sw.Stop();
                logger.LogWarning(
                    "Timed out loading field options for filter {FilterName} after {TimeoutSeconds}s",
                    filter.Name,
                    timeout.TotalSeconds);
                fieldOptions[filter.Name] = [];
            }
            catch (Exception ex)
            {
                sw.Stop();
                logger.LogWarning(ex, "Failed to load field options for filter {FilterName}", filter.Name);
                fieldOptions[filter.Name] = [];
            }
        }

        return fieldOptions;
    }
}
