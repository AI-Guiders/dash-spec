namespace DashSpec.Execution.Parsing;

internal static class DiagramModuleParser
{
    public static UnitFragment ParseDiagramFile(string text, string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return ModuleParseRegistration.ParseDiagramFile(text);
    }

    public static (string Id, UnitFragment Fragment) ParseDiagramFileWithId(string text, string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return ModuleParseRegistration.ParseDiagramFileWithId(text);
    }
}
