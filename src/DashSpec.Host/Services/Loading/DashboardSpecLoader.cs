using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Compilation;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
using DashSpec.Host.Configuration;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Models;

namespace DashSpec.Host.Services.Loading;

public sealed class DashboardSpecLoader(
    ConnectorRegistry connectorRegistry,
    ConnectorPluginManifest pluginManifest,
    DashSpecHostContext hostContext,
    ILogger<DashboardSpecLoader> logger) : IDashboardSpecLoader
{
    public async Task<LoadedDashboard> LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel,
        CancellationToken cancellationToken = default)
    {
        var configPath = DashSpecBootstrap.ResolveRuntimeConfigPath(
            specFullPath,
            text,
            hostContext.DefaultSpecDirectory);
        if (!string.Equals(configPath, hostContext.StartupRuntimeConfigPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Этот дашборд ссылается на другой @runtime ({Path.GetFileName(configPath)}). " +
                "Смена runtime-конфига в UI пока не поддерживается — укажи spec_path в dash-spec.toml и перезапусти Host.");
        }

        var document = DashSpecParser.Parse(text, Path.GetDirectoryName(specFullPath));
        var library = SpecLibraryComposer.Load(
            specFullPath,
            document.DiagramLibraryPath,
            document.PalettePath,
            hostContext.DefaultSpecDirectory);
        _ = SpecResolver.Resolve(document, library);
        var connector = connectorRegistry.Resolve(document.ConnectorId, pluginManifest.DefaultConnectorId);
        var filterIndex = DashboardBootstrap.IndexFilters(document);
        var filters = DashboardBootstrap.CreateInitialFilters(document, DateOnly.FromDateTime(DateTime.UtcNow));
        var fieldOptions = await LoadFieldOptionsAsync(document, connector, cancellationToken).ConfigureAwait(false);

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

    private async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadFieldOptionsAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        CancellationToken cancellationToken)
    {
        var fieldOptions = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var filter in document.Filters.Where(x => x.Kind is FilterKind.Field))
        {
            try
            {
                var sql = QueryCompiler.BuildDistinctFieldSql(filter);
                fieldOptions[filter.Name] = await connector
                    .QueryDistinctStringsAsync(sql, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load field options for filter {FilterName}", filter.Name);
                fieldOptions[filter.Name] = [];
            }
        }

        return fieldOptions;
    }
}
