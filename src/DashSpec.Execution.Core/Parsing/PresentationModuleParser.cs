namespace DashSpec.Execution.Parsing;

internal static class PresentationModuleParser
{
    public static PresentationBlock ParsePresentationFile(string text) =>
        ParsePresentationFile(text, null);

    public static (string Id, PresentationBlock Block) ParsePresentationFileWithId(string text, string? baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return ModuleParseRegistration.ParsePresentationFileWithId(text);
    }

    public static PresentationBlock ParsePresentationFile(string text, string? baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return ModuleParseRegistration.ParsePresentationBlock(text);
    }
}
