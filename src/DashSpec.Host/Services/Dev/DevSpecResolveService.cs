using DashSpec.Core.Parsing;
using DashSpec.Core.Resolution;
using DashSpec.Host.Configuration;

namespace DashSpec.Host.Services.Dev;

public sealed class DevSpecResolveService(
    DashSpecHostContext hostContext,
    IWebHostEnvironment environment)
{
    public DevSpecResolveResult ResolveConfiguredSpec()
    {
        var relative = hostContext.DefaultSpecRelativePath;
        if (string.IsNullOrWhiteSpace(relative))
        {
            return DevSpecResolveResult.Fail("Dashboard spec path is not configured.");
        }

        var specPath = DashSpecBootstrap.ResolveSpecPath(environment.ContentRootPath, relative);
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
            var document = DashSpecParser.Parse(text, Path.GetDirectoryName(specFullPath));
            var library = SpecLibraryComposer.Load(
                specFullPath,
                document.DiagramLibraryPath,
                document.PalettePath,
                hostContext.DefaultSpecDirectory);

            var export = SpecResolveExporter.Export(document, library);
            return DevSpecResolveResult.Ok(export, specFullPath, document.DiagramLibraryPath);
        }
        catch (Exception ex)
        {
            return DevSpecResolveResult.Fail(ex.Message);
        }
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
