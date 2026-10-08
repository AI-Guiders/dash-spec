using DashSpec.Core.Parsing;
using DashSpec.Core.Platform;
using DashSpec.Execution.Resolution;
using DashSpec.Viewer.Plugins;
using DashSpec.Host.Configuration;
using DashSpec.Abstractions.Hosting;
using DashSpec.Host.Services.Abstractions;

namespace DashSpec.Host.Services.Dev;

public sealed class DevSpecResolveService(
    DashSpecHostContext hostContext,
    IWebHostEnvironment environment,
    IHostPathResolver pathResolver,
    DashSpecParseOptionsProvider parseOptionsProvider,
    IReportCompiler reportCompiler)
{
    public DevSpecResolveResult ResolveConfiguredSpec()
    {
        var relative = hostContext.DefaultSpecRelativePath;
        if (string.IsNullOrWhiteSpace(relative))
        {
            return DevSpecResolveResult.Fail("Dashboard spec path is not configured.");
        }

        var specPath = pathResolver.ResolveSpecPath(environment.ContentRootPath, relative);
        return ResolveFile(specPath);
    }

    public DevSpecResolveResult ResolveFile(string specFullPath)
    {
        try
        {
            if (!File.Exists(specFullPath))
            {
                return DevSpecResolveResult.Fail($"DashSpec file not found: {specFullPath}");
            }

            var text = File.ReadAllText(specFullPath);
            var document = reportCompiler.Compile(
                text,
                Path.GetDirectoryName(specFullPath),
                CreateDevResolveParseOptions()).Document;
            var library = SpecLibraryComposer.Load(
                specFullPath,
                document.DiagramLibraryPath,
                document.PalettePath,
                hostContext.DefaultSpecDirectory,
                document);

            var export = SpecResolveExporter.Export(document, library);
            return DevSpecResolveResult.Ok(export, specFullPath, document.DiagramLibraryPath);
        }
        catch (Exception ex)
        {
            return DevSpecResolveResult.Fail(ex.Message);
        }
    }

    /// <summary>Dev inspector: validate the configured module file without merging tab dashspecs (see <see cref="DashSpecParseOptions.Editor"/>).</summary>
    private DashSpecParseOptions CreateDevResolveParseOptions()
    {
        var runtime = parseOptionsProvider.CreateOptions();
        return new DashSpecParseOptions
        {
            MergeReferencedTabModules = false,
            TolerateIncompleteIncludes = true,
            LinkOnlyReferencedDiagramUnits = false,
            ModuleLinkMode = runtime.ModuleLinkMode,
            ExtensionBlockKeywords = runtime.ExtensionBlockKeywords,
            ExtensionBlockPluginIds = runtime.ExtensionBlockPluginIds,
            PhraseTemplates = runtime.PhraseTemplates,
            KnownActionHandlers = runtime.KnownActionHandlers,
            KnownInteractionHandlers = runtime.KnownInteractionHandlers,
        };
    }
}

public sealed record DevSpecResolveResult(
    bool Success,
    ResolvedSpecExport? Export,
    string? SpecPath,
    string? DiagramLibraryPath,
    string? Error)
{
    public static DevSpecResolveResult Ok(
        ResolvedSpecExport export,
        string specPath,
        string? diagramLibraryPath) =>
        new(true, export, specPath, diagramLibraryPath, null);

    public static DevSpecResolveResult Fail(string error) =>
        new(false, null, null, null, error);
}
