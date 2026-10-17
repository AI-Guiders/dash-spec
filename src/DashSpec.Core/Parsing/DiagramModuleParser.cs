
namespace DashSpec.Core.Parsing;

internal static class DiagramModuleParser
{
    public static UnitFragment ParseDiagramFile(string text, string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (DiagramParseBridge.ParseDiagramFile is { } parse)
        {
            return parse(text, baseDirectory);
        }

        throw new InvalidOperationException("Diagram parse bridge not registered.");
    }

    public static (string Id, UnitFragment Fragment) ParseDiagramFileWithId(string text, string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (DiagramParseBridge.ParseDiagramFileWithId is { } parse)
        {
            return parse(text, baseDirectory);
        }

        throw new InvalidOperationException("Diagram parse bridge not registered.");
    }
}
