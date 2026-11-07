using DashSpec.Abstractions.Connectors;
using DashSpec.Execution.Bootstrap;
using DashSpec.Execution.Runtime;
using DashSpec.Compiler.Contract;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DashSpec.Execution.Compilation.Tests;

using FilterKind = DashSpec.Modeling.Parse.Filter.FilterKind;

public sealed class ReportFieldOptionsLoaderDependsTests
{
    private sealed class CapturingConnector : IDataSourceConnector
    {
        public List<string> DistinctSqls { get; } = [];

        public string Id => "fake";

        public Task<IReadOnlyList<string>> QueryDistinctStringsAsync(string sql, CancellationToken cancellationToken = default)
        {
            DistinctSqls.Add(sql);
            return Task.FromResult<IReadOnlyList<string>>(["a", "b"]);
        }

        public Task<Abstractions.Data.TypedRowBatch> QueryAsync(Abstractions.Query.CompiledQuery query, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class PassthroughCache : IReportFieldOptionsCache
    {
        public List<string> Keys { get; } = [];

        public Task<IReadOnlyList<string>> GetOrLoadAsync(
            string cacheKey,
            Func<CancellationToken, Task<IReadOnlyList<string>>> loader,
            CancellationToken cancellationToken = default)
        {
            Keys.Add(cacheKey);
            return loader(cancellationToken);
        }
    }

    private static FilterDefinition Field(string name, string column, string? dependsOn = null) => new()
    {
        Kind = FilterKind.Field,
        Name = name,
        DefaultExpression = Microsoft.FSharp.Core.FSharpOption<string>.None,
        ColumnReference = Microsoft.FSharp.Core.FSharpOption<string>.Some(column),
        Label = Microsoft.FSharp.Core.FSharpOption<string>.None,
        Widget = Microsoft.FSharp.Core.FSharpOption<string>.None,
        Density = Microsoft.FSharp.Core.FSharpOption<string>.None,
        MinValue = Microsoft.FSharp.Core.FSharpOption<int>.None,
        MaxValue = Microsoft.FSharp.Core.FSharpOption<int>.None,
        GrainFilterName = Microsoft.FSharp.Core.FSharpOption<string>.None,
        SingleSelect = false,
        LayoutRef = Microsoft.FSharp.Core.FSharpOption<string>.None,
        BindScopeHint = Microsoft.FSharp.Core.FSharpOption<string>.None,
        GrainLabels = Microsoft.FSharp.Core.FSharpOption<System.Collections.Generic.IReadOnlyDictionary<string, string>>.None,
        ApplyMode = "auto",
        Placement = Microsoft.FSharp.Core.FSharpOption<DashSpec.Modeling.Parse.Layout.PlacementDefinition>.None,
        DependsOn = Opt(dependsOn),
    };

    private static Microsoft.FSharp.Core.FSharpOption<string> Opt(string? value) =>
        value is null ? Microsoft.FSharp.Core.FSharpOption<string>.None : Microsoft.FSharp.Core.FSharpOption<string>.Some(value);

    private static DashboardDocument DocumentWithDepends(string productDependsOn)
    {
        var filters = new List<FilterDefinition>
        {
            Field("platform", "v_activity.platform"),
            Field("product", "v_activity.product", productDependsOn),
        };

        return new DashboardDocument
        {
            Id = "demo",
            Title = "Demo",
            ConnectorId = Microsoft.FSharp.Core.FSharpOption<string>.None,
            SqlDialect = SqlDialect.TSql,
            DiagramLibraryPath = Microsoft.FSharp.Core.FSharpOption<string>.None,
            Layout = new DashSpec.Modeling.Parse.Layout.LayoutDefinition { Columns = 12 },
            FiltersChrome = new FiltersChromeDefinition(),
            Filters = filters,
            DashboardFilters = [],
            Cards = [],
        };
    }

    private static ReportFieldOptionsLoader Loader(PassthroughCache cache) =>
        new(cache, new NullLogger<ReportFieldOptionsLoader>());

    [Fact]
    public async Task bootstrap_without_selection_keeps_full_domain()
    {
        var cache = new PassthroughCache();
        var connector = new CapturingConnector();

        var options = await Loader(cache).LoadAsync(
            DocumentWithDepends("platform"), connector, "rt", CancellationToken.None, TimeSpan.FromSeconds(5));

        Assert.Equal(2, options.Count);
        Assert.DoesNotContain("IN (", string.Join(';', connector.DistinctSqls));
    }

    [Fact]
    public async Task reload_with_platform_selection_narrows_the_product_domain()
    {
        var cache = new PassthroughCache();
        var connector = new CapturingConnector();
        var selections = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["platform"] = ["Linux"],
        };

        await Loader(cache).LoadAsync(
            DocumentWithDepends("platform"), connector, "rt", CancellationToken.None, TimeSpan.FromSeconds(5), selections);

        var productSql = connector.DistinctSqls.Single(s => s.Contains(" product ", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("v_activity.platform IN ('Linux')", productSql);
    }

    [Fact]
    public async Task different_selections_produce_different_cache_keys()
    {
        var cache = new PassthroughCache();
        var connector = new CapturingConnector();
        var document = DocumentWithDepends("platform");

        await Loader(cache).LoadAsync(document, connector, "rt", CancellationToken.None, TimeSpan.FromSeconds(5),
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["platform"] = ["Linux"] });
        await Loader(cache).LoadAsync(document, connector, "rt", CancellationToken.None, TimeSpan.FromSeconds(5),
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase) { ["platform"] = ["Windows"] });

        // the narrowed SQL rides the cache key, so different selections never alias
        var narrowedKeys = cache.Keys.Where(k => k.Contains("IN (", StringComparison.OrdinalIgnoreCase)).Distinct().ToList();
        Assert.Equal(2, narrowedKeys.Count);
        Assert.Contains(narrowedKeys, k => k.Contains("'Linux'", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(narrowedKeys, k => k.Contains("'Windows'", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task report_prefix_selection_resolves_the_same_dictionary()
    {
        var cache = new PassthroughCache();
        var connector = new CapturingConnector();
        var selections = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["platform"] = ["macOS"],
        };

        await Loader(cache).LoadAsync(
            DocumentWithDepends("report.platform"), connector, "rt", CancellationToken.None, TimeSpan.FromSeconds(5), selections);

        var productSql = connector.DistinctSqls.Single(s => s.Contains(" product ", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("v_activity.platform IN ('macOS')", productSql);
    }
}
