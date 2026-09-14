using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class PresentationModuleParser
{
    public static PresentationBlock ParsePresentationFile(string text) =>
        ParsePresentationFile(text, null);

    public static (string Id, PresentationBlock Block) ParsePresentationFileWithId(string text, string? baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (PresentationParseBridge.ParsePresentationFileWithId is { } parse)
        {
            return parse(text, baseDirectory);
        }

        throw new InvalidOperationException("Presentation parse bridge not registered.");
    }

    public static PresentationBlock ParsePresentationFile(string text, string? baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (PresentationParseBridge.ParsePresentationFile is { } parse)
        {
            return parse(text, baseDirectory);
        }

        throw new InvalidOperationException("Presentation parse bridge not registered.");
    }

    internal static bool IsChartChromeIncludeKind(string kind) =>
        string.Equals(kind, "presentation", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(kind, "chrome", StringComparison.OrdinalIgnoreCase);
}
