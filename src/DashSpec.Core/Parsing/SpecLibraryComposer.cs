namespace DashSpec.Core.Parsing;

/// <summary>Loads and merges diagram library TOML and <c>.dashpalette</c> modules.</summary>
public static class SpecLibraryComposer
{
    public static SpecLibrary? Load(
        string specFullPath,
        string? diagramLibraryPath,
        string? palettePath,
        string? fallbackDirectory = null)
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

        return library;
    }
}
