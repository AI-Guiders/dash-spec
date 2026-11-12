namespace DashSpec.Execution.Parsing;

internal static class PaletteModuleParser
{
    public static (string Id, IReadOnlyDictionary<string, string> Properties) ParsePaletteFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return ModuleParseRegistration.ParsePaletteFile(text);
    }
}
