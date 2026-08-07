using System.Diagnostics;
using DashSpec.Core.Compilation;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Core.Runtime;
using DashSpec.Host.Configuration;
using DashSpec.Host.Plugins;
using DashSpec.Host.Services.Abstractions;
using DashSpec.Host.Services.Connectors;
using DashSpec.Host.Services.Loading;

namespace DashSpec.Host.Services.Diagnostics;

public sealed class LoadDiagnosticsService(
    DashSpecHostContext hostContext,
    IWebHostEnvironment environment,
    DashSpecParseOptionsProvider parseOptionsProvider,
    RuntimeConnectorResolver runtimeConnectorResolver,
    IDashboardSpecLoader specLoader)
{
    public LoadDiagnosticsReport DiagnoseConfiguredSpec(bool includeCards = false, bool includeFieldOptions = true)
    {
        var relative = hostContext.DefaultSpecRelativePath;
        if (string.IsNullOrWhiteSpace(relative))
        {
            return LoadDiagnosticsReport.Fail("ui", "Dashboard spec path is not configured.", []);
        }

        var specPath = DashSpecBootstrap.ResolveSpecPath(environment.ContentRootPath, relative);
        return DiagnoseFile(specPath, includeCards, includeFieldOptions);
    }

    public LoadDiagnosticsReport DiagnoseCatalogEntry(
        string entryId,
        bool includeCards = false,
        bool includeFieldOptions = true)
    {
        try
        {
            var specPath = hostContext.Catalog.ResolveEntrySpecFullPath(entryId);
            return DiagnoseFile(specPath, includeCards, includeFieldOptions, entryId);
        }
        catch (Exception ex)
        {
            return LoadDiagnosticsReport.Fail($"catalog:{entryId}", ex.Message, []);
        }
    }

    public LoadDiagnosticsReport DiagnoseFile(
        string specFullPath,
        bool includeCards = false,
        bool includeFieldOptions = true,
        string? catalogEntryId = null)
    {
        var source = string.IsNullOrWhiteSpace(catalogEntryId)
            ? Path.GetFileName(specFullPath)
            : $"{catalogEntryId}:{Path.GetFileName(specFullPath)}";
        var steps = new List<LoadStepReport>();
        var total = Stopwatch.StartNew();

        try
        {
            if (!File.Exists(specFullPath))
            {
                return LoadDiagnosticsReport.Fail(source, $"DashSpec file not found: {specFullPath}", steps);
            }

            var readSw = Stopwatch.StartNew();
            var text = File.ReadAllText(specFullPath);
            readSw.Stop();
            steps.Add(new LoadStepReport("read_file", true, readSw.ElapsedMilliseconds, $"{text.Length} chars"));

            var parseSw = Stopwatch.StartNew();
            var document = DashSpecParser.Parse(
                text,
                Path.GetDirectoryName(specFullPath),
                parseOptionsProvider.CreateOptions());
            parseSw.Stop();
            steps.Add(new LoadStepReport(
                "parse_spec",
                true,
                parseSw.ElapsedMilliseconds,
                $"{document.Cards.Count} cards, {document.Filters.Count} filters"));

            var librarySw = Stopwatch.StartNew();
            var library = SpecLibraryComposer.Load(
                specFullPath,
                document.DiagramLibraryPath,
                document.PalettePath,
                hostContext.DefaultSpecDirectory,
                document);
            librarySw.Stop();
            steps.Add(new LoadStepReport("load_library", true, librarySw.ElapsedMilliseconds, document.DiagramLibraryPath));

            var resolveSw = Stopwatch.StartNew();
            _ = SpecResolver.Resolve(document, library);
            resolveSw.Stop();
            steps.Add(new LoadStepReport("resolve_model", true, resolveSw.ElapsedMilliseconds));

            var runtimeSw = Stopwatch.StartNew();
            var runtimePath = DashSpecBootstrap.ResolveRuntimeConfigPath(
                specFullPath,
                text,
                hostContext.DefaultSpecDirectory);
            runtimeSw.Stop();
            var sameAsStartup = string.Equals(
                runtimePath,
                hostContext.StartupRuntimeConfigPath,
                StringComparison.OrdinalIgnoreCase);
            steps.Add(new LoadStepReport(
                "runtime_manifest",
                true,
                runtimeSw.ElapsedMilliseconds,
                Path.GetFileName(runtimePath),
                sameAsStartup ? "startup default" : "per-entry runtime"));

            var connectorSw = Stopwatch.StartNew();
            var connector = runtimeConnectorResolver.Resolve(runtimePath, document.ConnectorId);
            connectorSw.Stop();
            steps.Add(new LoadStepReport("resolve_connector", true, connectorSw.ElapsedMilliseconds, connector.Id));

            if (includeFieldOptions)
            {
                foreach (var filter in document.Filters.Where(x => x.Kind is FilterKind.Field))
                {
                    var filterSw = Stopwatch.StartNew();
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                        var values = specLoader.LoadFieldOptionsAsync(document, connector, cts.Token)
                            .GetAwaiter()
                            .GetResult();
                        filterSw.Stop();
                        var count = values.TryGetValue(filter.Name, out var loaded) ? loaded.Count : 0;
                        steps.Add(new LoadStepReport(
                            $"field_options:{filter.Name}",
                            true,
                            filterSw.ElapsedMilliseconds,
                            $"{count} values"));
                    }
                    catch (Exception ex)
                    {
                        filterSw.Stop();
                        steps.Add(new LoadStepReport(
                            $"field_options:{filter.Name}",
                            false,
                            filterSw.ElapsedMilliseconds,
                            filter.ColumnReference,
                            ex.Message));
                    }
                }
            }

            if (includeCards)
            {
                var loaded = specLoader.LoadFromTextAsync(
                        text,
                        specFullPath,
                        Path.GetFileName(specFullPath),
                        options: new SpecLoadOptions { LoadFieldOptions = includeFieldOptions })
                    .GetAwaiter()
                    .GetResult();

                foreach (var card in loaded.Document.Cards)
                {
                    var cardSw = Stopwatch.StartNew();
                    try
                    {
                        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                        _ = loaded.Connector.QueryAsync(
                                QueryCompiler.Compile(
                                    card,
                                    loaded.Filters,
                                    loaded.FilterIndex,
                                    loaded.Document.SqlDialect,
                                    loaded.SpecDirectory),
                                cts.Token)
                            .GetAwaiter()
                            .GetResult();
                        cardSw.Stop();
                        steps.Add(new LoadStepReport(
                            $"render_query:{card.Id}",
                            true,
                            cardSw.ElapsedMilliseconds,
                            card.Diagram.Kind.ToString()));
                    }
                    catch (Exception ex)
                    {
                        cardSw.Stop();
                        steps.Add(new LoadStepReport(
                            $"render_query:{card.Id}",
                            false,
                            cardSw.ElapsedMilliseconds,
                            card.DataSource.Value,
                            ex.Message));
                    }
                }
            }

            total.Stop();
            return new LoadDiagnosticsReport(
                source,
                true,
                specFullPath,
                catalogEntryId,
                total.ElapsedMilliseconds,
                null,
                steps);
        }
        catch (Exception ex)
        {
            total.Stop();
            return LoadDiagnosticsReport.Fail(
                source,
                ex.Message,
                steps,
                specFullPath,
                catalogEntryId,
                total.ElapsedMilliseconds);
        }
    }

    public async Task<LoadStepReport> PingConnectorAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var connector = runtimeConnectorResolver.Resolve(
                hostContext.StartupRuntimeConfigPath,
                connectorId: null);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));
            await connector.QueryDistinctStringsAsync("SELECT CAST(1 AS varchar(1))", cts.Token).ConfigureAwait(false);
            sw.Stop();
            return new LoadStepReport("ping_connector", true, sw.ElapsedMilliseconds, connector.Id);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new LoadStepReport("ping_connector", false, sw.ElapsedMilliseconds, null, ex.Message);
        }
    }
}

public sealed record LoadDiagnosticsReport(
    string Source,
    bool Success,
    string? SpecPath,
    string? CatalogEntryId,
    long TotalElapsedMs,
    string? Error,
    IReadOnlyList<LoadStepReport> Steps)
{
    public static LoadDiagnosticsReport Fail(
        string source,
        string error,
        IReadOnlyList<LoadStepReport> steps,
        string? specPath = null,
        string? catalogEntryId = null,
        long totalElapsedMs = 0) =>
        new(source, false, specPath, catalogEntryId, totalElapsedMs, error, steps);
}
