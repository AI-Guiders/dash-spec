using DashSpec.Compiler.Contract;
using DashSpec.Execution.Parsing;
using DashSpec.Execution.Runtime;
using CatalogDocument = DashSpec.Modeling.Parse.Catalog.CatalogDocument;
using ExecutionDashSpecParser = DashSpec.Execution.Parsing.DashSpecParser;
using HostDocument = DashSpec.Modeling.Parse.Host.HostDocument;
using LayoutBoardDefinition = DashSpec.Modeling.Parse.Layout.LayoutBoardDefinition;
using RuntimeDocument = DashSpec.Modeling.Parse.Runtime.RuntimeDocument;

namespace DashSpec.Execution.Compilation;

/// <summary>C# language &amp; compile adapter over F# <c>DashSpecCompiler</c> and <c>UnitProjector</c> (ADR-0099 B4).</summary>
public sealed class ReportCompiler : IReportCompiler
{
    public ReportCompileResult Compile(string text, string? specDirectory, DashSpecParseOptions parseOptions) =>
        ExecutionDashSpecParser.Compile(text, specDirectory, parseOptions);

    public ReportCompileResult Compile(string text, string? specDirectory) =>
        ExecutionDashSpecParser.Compile(text, specDirectory);

    public string? ReadRuntimePath(string text) =>
        ExecutionDashSpecParser.ReadRuntimePath(text);

    public HostDocument ParseHost(string text, string? specDirectory = null) =>
        ExecutionDashSpecParser.ParseHost(text, specDirectory);

    public HostDocument ParseHostFile(string path) =>
        ExecutionDashSpecParser.ParseHostFile(path);

    public CatalogDocument ParseCatalog(string text) =>
        ExecutionDashSpecParser.ParseCatalog(text);

    public CatalogDocument ParseCatalogFile(string path) =>
        CatalogReader.ParseFile(path);

    public RuntimeDocument ParseRuntime(string text)
    {
        ExecutionDashSpecParser.EnsureModuleParsersRegistered();
        return DashSpec.Modeling.Parse.Runtime.RuntimeModuleParser.parse(text);
    }

    public LayoutBoardDefinition ParseLayout(string text) =>
        ExecutionDashSpecParser.ParseLayoutFile(text);

    public string? TryDiscoverHostSpecPath(string directory, bool skipLocalConfig = false)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        ExecutionDashSpecParser.EnsureModuleParsersRegistered();
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
}
