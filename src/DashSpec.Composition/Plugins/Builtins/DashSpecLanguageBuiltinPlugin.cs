using DashSpec.Abstractions.Plugins;
using DashSpec.Compiler.Contract;
using DashSpec.Execution.Parsing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CatalogDocument = DashSpec.Modeling.Parse.Catalog.CatalogDocument;
using HostDocument = DashSpec.Modeling.Parse.Host.HostDocument;
using LayoutBoardDefinition = DashSpec.Modeling.Parse.Layout.LayoutBoardDefinition;
using RuntimeDocument = DashSpec.Modeling.Parse.Runtime.RuntimeDocument;

namespace DashSpec.Composition.Plugins.Builtins;

/// <summary>
/// Built-in <c>.ds</c> language &amp; compiler front-end plugin (<c>language_ds</c>):
/// registers <see cref="IReportCompiler"/> into the pipeline so that <c>.ds</c> reading,
/// unit projection, and report compilation participate in the same plugin kernel as
/// connectors, diagram kinds, and viz renderers.
/// </summary>
public sealed class DashSpecLanguageBuiltinPlugin : IDashSpecPlugin, IReportCompiler
{
    public const string PluginIdValue = "language_ds";

    public static DashSpecLanguageBuiltinPlugin Instance { get; } = new();

    public string Id => PluginIdValue;

    public string DisplayName => "DashSpec (.ds) language & compiler front-end";

    public PluginTier Tier => PluginTier.Core;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<IReportCompiler>(this);
    }

    public void RegisterContributors(IDashSpecContributorRegistry registry)
    {
    }

    public ReportCompileResult Compile(string text, string? specDirectory, DashSpecParseOptions parseOptions) =>
        DashSpecParser.Compile(text, specDirectory, parseOptions);

    public ReportCompileResult Compile(string text, string? specDirectory) =>
        DashSpecParser.Compile(text, specDirectory);

    public string? ReadRuntimePath(string text) =>
        DashSpecParser.ReadRuntimePath(text);

    public HostDocument ParseHost(string text, string? specDirectory = null) =>
        DashSpecParser.ParseHost(text, specDirectory);

    public HostDocument ParseHostFile(string path) =>
        DashSpecParser.ParseHostFile(path);

    public CatalogDocument ParseCatalog(string text) =>
        DashSpecParser.ParseCatalog(text);

    public CatalogDocument ParseCatalogFile(string path) =>
        CatalogReader.ParseFile(path);

    public RuntimeDocument ParseRuntime(string text)
    {
        DashSpecParser.EnsureModuleParsersRegistered();
        return DashSpec.Modeling.Parse.Runtime.RuntimeModuleParser.parse(text);
    }

    public LayoutBoardDefinition ParseLayout(string text) =>
        DashSpecParser.ParseLayoutFile(text);

    public string? TryDiscoverHostSpecPath(string directory, bool skipLocalConfig = false)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        DashSpecParser.EnsureModuleParsersRegistered();
        var idx = DashSpec.Modeling.Parse.Document.UnitIndexModule.build(directory);
        var cfgOpt = DashSpec.Modeling.Parse.Project.DashSpecProjectLocator.tryFindConfig(
            Microsoft.FSharp.Core.FSharpOption<string>.Some(directory));
        if (Microsoft.FSharp.Core.FSharpOption<DashSpec.Modeling.Parse.Project.DashSpecProjectConfig>.get_IsSome(cfgOpt) &&
            Microsoft.FSharp.Core.FSharpOption<string>.get_IsSome(cfgOpt.Value.HostId))
        {
            var explicitHostOpt = idx.TryFindUnit("host", cfgOpt.Value.HostId.Value);
            if (Microsoft.FSharp.Core.FSharpOption<DashSpec.Modeling.Parse.Document.UnitIndexEntry>.get_IsSome(explicitHostOpt) &&
                File.Exists(explicitHostOpt.Value.Path))
            {
                return Path.GetFullPath(explicitHostOpt.Value.Path);
            }
        }

        var hostEntry = idx.Entries.FirstOrDefault(e =>
            e.Kind is not null &&
            string.Equals(e.Kind.Value, "host", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(e.Path) &&
            (!skipLocalConfig || !Path.GetFileName(e.Path).Contains(".local.", StringComparison.OrdinalIgnoreCase)));

        return hostEntry is not null ? Path.GetFullPath(hostEntry.Path) : null;
    }

    public string ReadEmbeddedUnit(string logicalPath)
    {
        DashSpecParser.EnsureModuleParsersRegistered();
        return DashSpec.Modeling.Parse.Document.UnitSource.readUnit(logicalPath);
    }
}
