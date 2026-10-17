
namespace DashSpec.Core.Parsing;

/// <summary>Transitional entry — delegates to F# Modeling.Parse via <see cref="LayoutParseBridge"/>.</summary>
public static class LayoutModuleParser
{
    public static LayoutBoardDefinition ParseLayoutFile(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (LayoutParseBridge.ParseLayoutFile is { } parse)
        {
            return parse(text);
        }

        throw new InvalidOperationException(
            "Layout parse bridge not registered. Reference DashSpec.Execution.Core or register LayoutParseBridge.ParseLayoutFile.");
    }
}