namespace DashSpec.Core.Parsing;

internal static class PaletteModuleParser
{
    public static (string Id, IReadOnlyDictionary<string, string> Properties) ParsePaletteFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (PaletteParseBridge.ParsePaletteFile is { } parse)
        {
            return parse(text);
        }

        throw new InvalidOperationException("Palette parse bridge not registered.");
    }

    public static SpecLibrary LoadPaletteFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Palette file not found: {path}", path);
        }

        var (id, props) = ParsePaletteFile(File.ReadAllText(path));
        return SpecLibrary.FromPalette(id, props);
    }
}
