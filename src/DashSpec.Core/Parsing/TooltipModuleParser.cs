using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class TooltipModuleParser
{
    public static TooltipDefinition ParseTooltipFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (TooltipParseBridge.ParseTooltipFile is { } parse)
        {
            return parse(text);
        }

        throw new InvalidOperationException("Tooltip parse bridge not registered.");
    }

    public static (string Id, TooltipDefinition Definition) ParseTooltipFileWithId(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (TooltipParseBridge.ParseTooltipFileWithId is { } parse)
        {
            return parse(text);
        }

        throw new InvalidOperationException("Tooltip parse bridge not registered.");
    }

    public static TooltipDefinition ParseInline(TokenReader reader, string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (TooltipParseBridge.ParseTooltipBody is { } parse)
        {
            return parse(id, reader.ReadTooltipBodySource());
        }

        throw new InvalidOperationException("Tooltip parse bridge not registered.");
    }
}
