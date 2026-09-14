using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class DiagramModuleParser
{
    public static SpecIncludeFragment ParseDiagramFile(string text, string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (DiagramParseBridge.ParseDiagramFile is { } parse)
        {
            return parse(text, baseDirectory);
        }

        throw new InvalidOperationException("Diagram parse bridge not registered.");
    }

    public static (string Id, SpecIncludeFragment Fragment) ParseDiagramFileWithId(string text, string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (DiagramParseBridge.ParseDiagramFileWithId is { } parse)
        {
            return parse(text, baseDirectory);
        }

        throw new InvalidOperationException("Diagram parse bridge not registered.");
    }

    internal static (string Kind, string Reference) ReadIncludeReference(TokenReader reader)
    {
        var kind = reader.ReadIdent();
        var reference = reader.ReadString();
        return (kind, reference);
    }
}
