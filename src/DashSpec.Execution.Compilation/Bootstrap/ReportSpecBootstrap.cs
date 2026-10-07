using DashSpec.Abstractions.Connectors;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Platform;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Runtime;
using DashSpec.Execution.Resolution;
using DashSpec.Execution.Runtime.Platform;
using DashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;

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
                "В .dashspec нет @runtime — укажите runtime { manifest = \"...\" } в блоке @dashboard/@tab.");
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
            document.DiagramLibraryPath,
            document.PalettePath,
            environment.DefaultSpecDirectory,
            document);
        _ = SpecResolver.Resolve(document, library);
        var connector = connectorResolver.Resolve(configPath, document.ConnectorId);
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
            configPath);
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
