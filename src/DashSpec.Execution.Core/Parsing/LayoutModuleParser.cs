namespace DashSpec.Execution.Parsing;

public static class LayoutModuleParser
{
    public static LayoutBoardDefinition ParseLayoutFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return ModuleParseRegistration.ParseLayoutFile(text);
    }
}
