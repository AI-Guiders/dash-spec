using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class TooltipParseBridge
{
    internal static Func<string, TooltipDefinition>? ParseTooltipFile { get; set; }

    internal static Func<string, (string Id, TooltipDefinition Definition)>? ParseTooltipFileWithId { get; set; }
}
