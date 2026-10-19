
namespace DashSpec.Execution.Parsing;

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
}
