using DashSpec.Abstractions.Connectors;
using DashSpec.Execution.Parsing;
using DashSpec.Execution.Runtime;
using DashSpec.Execution.Resolution;
using DashSpec.Execution.Runtime.Platform;
using DashSpec.Modeling.Parse.Layout;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;

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
        var entryRuntime = DashSpecParser.ReadRuntimePath(text);
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
        var compile = reportCompiler.Compile(text, specDirectory, parseOptionsSource.CreateOptions());
        var document = compile.Document;
        var library = SpecLibraryComposer.Load(
            specFullPath,
            document.DiagramLibraryPath?.Value,
            environment.DefaultSpecDirectory,
            document);
        _ = SpecResolver.Resolve(document, library);

        // Assembled slot plane ([ADR-0111]): every visual slice precomputed once per document
        // revision — the render reads it and places. Correspondence failures are loud ([ADR-0109]):
        // a slot whose occupant has no document-graph node fails the load with the exact node id.
        var slotPlane = SlotPlaneBuilder.Build(document, library);

        var connector = connectorResolver.Resolve(configPath, document.ConnectorId?.Value);
        var filterIndex = DashboardBootstrap.IndexFilters(document);
        var filters = DashboardBootstrap.CreateInitialFilters(document, DateOnly.FromDateTime(DateTime.UtcNow));
        var fieldOptions = options.LoadFieldOptions
            ? await fieldOptionsLoader
                .LoadAsync(document, connector, configPath, cancellationToken, options.FieldOptionsTimeout)
                .ConfigureAwait(false)
            : new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

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

    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> LoadFieldOptionsAsync(
        DashboardDocument document,
        IDataSourceConnector connector,
        string runtimeConfigPath,
        CancellationToken cancellationToken = default,
        TimeSpan? timeout = null) =>
        fieldOptionsLoader.LoadAsync(
            document,
            connector,
            runtimeConfigPath,
            cancellationToken,
            timeout ?? TimeSpan.FromSeconds(20));
}
