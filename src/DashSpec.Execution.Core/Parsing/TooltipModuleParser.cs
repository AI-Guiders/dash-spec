namespace DashSpec.Execution.Parsing;

internal static class TooltipModuleParser
{
    public static TooltipDefinition ParseTooltipFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return ModuleParseRegistration.ParseTooltipFile(text);
    }

    public static (string Id, TooltipDefinition Definition) ParseTooltipFileWithId(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return ModuleParseRegistration.ParseTooltipFileWithId(text);
    }

    public static TooltipDefinition ParseInline(TokenReader reader, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return ModuleParseRegistration.ParseTooltipBody(id, Modeling.Parse.TokenReaderTooltip.readBodySource(reader));
    }
}
