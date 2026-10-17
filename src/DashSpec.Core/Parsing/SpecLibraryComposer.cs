namespace DashSpec.Core.Parsing;


/// <summary>Loads and merges diagram library TOML, the document palette and units imported into the module (ADR-0098).</summary>
public static class SpecLibraryComposer
{
    public static SpecLibrary? Load(
        string specFullPath,
        string? diagramLibraryPath,
        string? fallbackDirectory = null,
        DashboardDocument? document = null)
    {
        SpecLibrary? library = null;

        if (!string.IsNullOrWhiteSpace(diagramLibraryPath))
        {
            var path = SpecPathResolver.ResolveNearSpec(specFullPath, diagramLibraryPath, fallbackDirectory);
            library = SpecLibrary.Merge(library, SpecLibrary.LoadFile(path));
        }

        if (document?.Palette?.Value is { } palette)
        {
            library = SpecLibrary.Merge(library, SpecLibrary.FromPalette(palette.Id, palette.Properties));
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
