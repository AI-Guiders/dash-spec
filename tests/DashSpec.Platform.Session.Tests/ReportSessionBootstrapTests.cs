using DashSpec.Abstractions.Connectors;
using DashSpec.Abstractions.Data;
using DashSpec.Abstractions.Query;
using DashSpec.Core.Model;
using DashSpec.Core.Parsing;
using DashSpec.Core.Platform;
using DashSpec.Core.Runtime;
using DashSpec.Execution.Bootstrap;
using DashSpec.Execution.Compilation;
using DashSpec.Execution.Runtime.Platform;
using DashSpec.Execution.Runtime.Session;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DashSpec.Platform.Session.Tests;

public sealed class ReportSessionBootstrapTests
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
            var filters = new List<FilterDefinition>
            {
                new(FilterKind.Date, "usage_date", null, "t.usage_date")
            };
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
                filters,
                ["usage_date"],
                [],
                []);
            return new ReportCompileResult(doc, []);
        }
    }

    [Fact]
    public async Task ReportSession_loads_via_bootstrap_and_mutates_date_filter()
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

        var session = new ReportSession(bootstrap);
        var dir = Path.Combine(Path.GetTempPath(), "dashspec-l3-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "x.dashspec");
        await File.WriteAllTextAsync(path, text);

        await session.LoadFromTextAsync(text, path, options: new ReportLoadOptions { LoadFieldOptions = false });

        Assert.Equal("demo", session.Document.Id);
        session.ApplyDateFilter("usage_date", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var range = session.Filters.GetDate("usage_date");
        Assert.True(range.HasValue);
        Assert.Equal(new DateOnly(2026, 1, 1), range!.Value.From);
        Assert.Equal(new DateOnly(2026, 1, 31), range.Value.To);
    }
}
