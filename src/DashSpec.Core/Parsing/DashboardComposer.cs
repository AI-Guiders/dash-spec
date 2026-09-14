using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

/// <summary>Transitional entry — delegates to F# Modeling.Parse via <see cref="DocumentParseBridge"/>.</summary>
internal static class DashboardComposer
{
    public static DashboardDocument Parse(string text, string? specDirectory = null) =>
        Parse(text, specDirectory, DashSpecParseOptions.Default);

    public static DashboardDocument Parse(
        string text,
        string? specDirectory,
        DashSpecParseOptions parseOptions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (DocumentParseBridge.Parse is { } parse)
        {
            return parse(text, specDirectory, parseOptions);
        }

        throw new InvalidOperationException(
            "Document parse bridge not registered. Reference DashSpec.Execution.Core or register DocumentParseBridge.Parse.");
    }

    public static bool IsTabRootDocument(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (DocumentParseBridge.IsTabRootDocument is { } check)
        {
            return check(text);
        }

        throw new InvalidOperationException(
            "Document parse bridge not registered. Reference DashSpec.Execution.Core or register DocumentParseBridge.IsTabRootDocument.");
    }
}
