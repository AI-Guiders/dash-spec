namespace DashSpec.Execution.Parsing;

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
}
