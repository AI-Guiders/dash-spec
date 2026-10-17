
namespace DashSpec.Core.Parsing;

internal static class PaletteParseBridge
{
    internal static Func<string, (string Id, IReadOnlyDictionary<string, string> Properties)>? ParsePaletteFile { get; set; }
}
