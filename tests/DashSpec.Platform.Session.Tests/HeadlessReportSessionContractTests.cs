using DashSpec.Abstractions.Connectors;
using DashSpec.Abstractions.Data;
using DashSpec.Abstractions.Query;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Platform;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Bootstrap;
using DashSpec.Execution.Compilation;
using DashSpec.Execution.Runtime;
using DashSpec.Execution.Runtime.Platform;
using DashSpec.Viz;
using DashSpec.Viz.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DashSpec.Platform.Session.Tests;

public sealed class HeadlessReportSessionContractTests
{
    private sealed class FakeConnector : IDataSourceConnector
    {
        public string Id => "fake";
        public Task<IReadOnlyList<string>> QueryDistinctStringsAsync(string sql, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public Task<TypedRowBatch> QueryAsync(CompiledQuery query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FakeResolver : IReportConnectorResolver
    {
        public IDataSourceConnector Resolve(string runtimeConfigPath, string? connectorId) => new FakeConnector();
    }

    private sealed class FakePaths : IReportRuntimePaths
    {
        public string ResolveRuntimeConfigPath(string specFullPath, string specText, string defaultSpecDirectory) =>
            Path.Combine(defaultSpecDirectory, "fake-runtime.toml");
    }

    private sealed class FakeParseOptions : IReportParseOptionsSource
    {
        public DashSpecParseOptions CreateOptions() => new();
    }

    private sealed class FakeEnv : IReportBootstrapEnvironment
    {
        public string DefaultSpecDirectory { get; init; } = "";
    }

    private sealed class FakeFieldCache : IReportFieldOptionsCache
    {
        public Task<IReadOnlyList<string>> GetOrLoadAsync(
            string cacheKey,
            Func<CancellationToken, Task<IReadOnlyList<string>>> loader,
            CancellationToken cancellationToken = default) =>
            loader(cancellationToken);
    }

    private sealed class FakeCardRenderer : IReportCardRenderer
    {
        public Task<CardRenderResult> RenderAsync(
            CardDefinition card,
            DashboardDocument document,
            FilterState filters,
            IReadOnlyDictionary<string, FilterDefinition> filterIndex,
            SpecLibrary? library,
            IDataSourceConnector connector,
            string? specDirectory = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new CardRenderResult(
                card.Id,
                card.Title,
                card.Diagram.Kind,
                DiagramDataFamily.Scalar,
                "test.plugin"));

        public Task<IReadOnlyDictionary<string, CardSlotRenderResult>> RenderInteriorSlotsAsync(
            CardDefinition card,
            DashboardDocument document,
            FilterState filters,
            IReadOnlyDictionary<string, FilterDefinition> filterIndex,
            SpecLibrary? library,
            IDataSourceConnector connector,
            string? specDirectory = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<string, CardSlotRenderResult>>(
                new Dictionary<string, CardSlotRenderResult>());
    }

    private sealed class StubCompiler : IReportCompiler
    {
        public ReportCompileResult Compile(string text, string? specDirectory) =>
            Compile(text, specDirectory, new DashSpecParseOptions());

        public ReportCompileResult Compile(string text, string? specDirectory, DashSpecParseOptions parseOptions)
        {
            var card = new CardDefinition(
                "kpi",
                "KPI",
                new DiagramDefinition("number", new Dictionary<string, string>()),
                new DataSourceDefinition(DataSourceKind.View, "v_kpi"),
                [],
                []);
            var doc = new DashboardDocument(
                "demo",
                "Demo",
                "fake",
                SqlDialect.TSql,
                null,
                null,
                null,
                LayoutDefinition.Default,
                new FiltersChromeDefinition(),
                [],
                [],
                [],
                [card]);
            return new ReportCompileResult(doc, []);
        }
    }

    [Fact]
    public async Task IReportSession_headless_load_filter_and_render_with_fake_renderer()
    {
        const string text = """
            @dashboard demo
                runtime
                    manifest = "demo.toml"
                end runtime
            end dashboard
            """;
        var bootstrap = new ReportSpecBootstrap(
            new StubCompiler(),
            new FakePaths(),
            new FakeResolver(),
            new FakeParseOptions(),
            new FakeEnv { DefaultSpecDirectory = Path.GetTempPath() },
            new ReportFieldOptionsLoader(new FakeFieldCache(), NullLogger<ReportFieldOptionsLoader>.Instance));

        IReportSession session = new HeadlessReportSession(bootstrap, new FakeCardRenderer());
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-l3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "x.dashspec");
        await File.WriteAllTextAsync(path, text);

        var headless = (HeadlessReportSession)session;
        await headless.LoadFromTextAsync(text, path, options: new ReportLoadOptions { LoadFieldOptions = false });

        Assert.Equal("fake", session.ActiveConnectorId);
        Assert.Single(session.Document.Cards);

        var card = session.Document.Cards[0];
        var rendered = await session.RenderCardAsync(card);
        Assert.Equal("kpi", rendered.Id);
        Assert.Equal("test.plugin", rendered.RenderPluginId);
    }
}
