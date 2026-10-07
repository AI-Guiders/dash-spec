using DashSpec.Abstractions.Connectors;
using DashSpec.Abstractions.Data;
using DashSpec.Abstractions.Query;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Platform;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Bootstrap;
using DashSpec.Execution.Runtime.Platform;
using Xunit;

namespace DashSpec.Execution.Compilation.Tests;

public sealed class ReportSpecBootstrapTests
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

    private sealed class StubCompiler : IReportCompiler
    {
        public ReportCompileResult Compile(string text, string? specDirectory) =>
            Compile(text, specDirectory, new DashSpecParseOptions());

        public ReportCompileResult Compile(string text, string? specDirectory, DashSpecParseOptions parseOptions)
        {
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
                []);
            return new ReportCompileResult(doc, []);
        }
    }

    [Fact]
    public async Task Bootstrap_uses_compiler_and_connector_resolver()
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
            new ReportFieldOptionsLoader(new FakeFieldCache(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ReportFieldOptionsLoader>.Instance));

        var dir = Path.Combine(Path.GetTempPath(), "dashspec-bootstrap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "x.dashspec");
        await File.WriteAllTextAsync(path, text);

        var result = await bootstrap.LoadFromTextAsync(
            text,
            path,
            "test",
            options: new ReportLoadOptions { LoadFieldOptions = false });

        Assert.Equal("demo", result.Document.Id);
        Assert.Equal("fake", result.Connector.Id);
        Assert.Equal("test", result.SourceLabel);
    }
}
