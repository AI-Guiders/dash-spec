namespace DashSpec.Core.Parsing;

using DashSpec.Core.Model;

/// <summary>Loads and merges diagram library TOML, <c>.dashpalette</c>, and module <c>!include</c> diagrams.</summary>
public static class SpecLibraryComposer
{
    public static SpecLibrary? Load(
        string specFullPath,
        string? diagramLibraryPath,
        string? palettePath,
        string? fallbackDirectory = null,
        DashboardDocument? document = null)
    {
        SpecLibrary? library = null;

        if (!string.IsNullOrWhiteSpace(diagramLibraryPath))
        {
            var path = SpecPathResolver.ResolveNearSpec(specFullPath, diagramLibraryPath, fallbackDirectory);
            library = SpecLibrary.Merge(library, SpecLibrary.LoadFile(path));
        }

        if (!string.IsNullOrWhiteSpace(palettePath))
        {
            var path = SpecPathResolver.ResolveNearSpec(specFullPath, palettePath, fallbackDirectory);
            library = SpecLibrary.Merge(library, PaletteModuleParser.LoadPaletteFile(path));
        }

        if (document is not null &&
            (document.ResolvedChartChromePresets.Count > 0 || document.ResolvedModuleDiagrams.Count > 0))
        {
            library = SpecLibrary.Merge(
                library,
                SpecLibrary.FromModuleDocument(
                    document.ResolvedChartChromePresets,
                    document.ResolvedModuleDiagrams));
        }

        return library;
    }
}
