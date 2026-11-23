using DashSpec.Abstractions.Connectors;
using DashSpec.Execution.Parsing;
using DashSpec.Execution.Runtime;
using DashSpec.Execution.Resolution;
using DashSpec.Execution.Runtime.Platform;
using DashSpec.Modeling.Parse.Layout;

using DashSpec.Compiler.Contract;

namespace DashSpec.Execution.Bootstrap;

/// <summary>Platform spec bootstrap — compile, library, connector, filters (ADR-0099 B2).</summary>
public sealed class ReportSpecBootstrap(
    IReportCompiler reportCompiler,
    IReportRuntimePaths runtimePaths,
    IReportConnectorResolver connectorResolver,
    IReportParseOptionsSource parseOptionsSource,
    IReportBootstrapEnvironment environment,
    ReportFieldOptionsLoader fieldOptionsLoader) : IReportSpecBootstrap
{
    public async Task<ReportBootstrapResult> LoadFromTextAsync(
        string text,
        string specFullPath,
        string sourceLabel,
        CancellationToken cancellationToken = default,
        ReportLoadOptions? options = null)
    {
        options ??= new ReportLoadOptions();
        var entryRuntime = reportCompiler.ReadRuntimePath(text);
        if (string.IsNullOrWhiteSpace(entryRuntime))
        {
            throw new InvalidOperationException(
                "В модуле нет runtime — укажите runtime { manifest = \"...\" } рядом с report.");
        }

        var configPath = runtimePaths.ResolveRuntimeConfigPath(
            specFullPath,
            text,
            environment.DefaultSpecDirectory);

        var specDirectory = Path.GetDirectoryName(specFullPath);
        if (!DashSpec.Execution.Compilation.PrecompiledReportBundle.TryGetOrCompile(
                text,
                specFullPath,
                environment.DefaultSpecDirectory,
                reportCompiler,
                parseOptionsSource.CreateOptions,
                out var cached))
        {
            throw new InvalidOperationException(
                "В модуле нет runtime — укажите runtime { manifest = \"...\" } рядом с report.");
        }

        var document = cached.Document;
        var library = cached.Library;
        var slotPlane = cached.SlotPlane;

        var connector = connectorResolver.Resolve(configPath, document.ConnectorId?.Value);
        var filterIndex = DashboardBootstrap.IndexFilters(document);
        var filters = DashboardBootstrap.CreateInitialFilters(document, DateOnly.FromDateTime(DateTime.UtcNow));
        var fieldOptions = options.LoadFieldOptions
            ? await fieldOptionsLoader
                .LoadAsync(document, connector, configPath, cancellationToken, options.FieldOptionsTimeout)
                .ConfigureAwait(false)
            : FieldOptionsCatalog.Empty;

        return new ReportBootstrapResult(
            document,
            library,
            connector,
            filterIndex,
            filters,
            fieldOptions,
            sourceLabel,
            specDirectory,
            configPath,
            slotPlane);
    }

    public Task<FieldOptionsCatalog> LoadFieldOptionsAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        string runtimeConfigPath,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? currentSelections = null) =>
        fieldOptionsLoader.LoadAsync(
            document,
            connector,
            runtimeConfigPath,
            cancellationToken,
            timeout ?? TimeSpan.FromSeconds(20),
            currentSelections);
}
