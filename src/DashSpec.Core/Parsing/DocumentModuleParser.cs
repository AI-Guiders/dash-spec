using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

/// <summary>Transitional entry — delegates to F# Modeling.Parse via <see cref="DocumentParseBridge"/>.</summary>
internal static class DocumentModuleParser
{
    public static bool IsBlockModuleFormat(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (DocumentParseBridge.IsBlockModuleFormat is { } check)
        {
            return check(text);
        }

        throw new InvalidOperationException(
            "Document parse bridge not registered. Reference DashSpec.Execution.Core or register DocumentParseBridge.IsBlockModuleFormat.");
    }

    public static DashboardDocument ParseDocument(string text, string? specDirectory = null) =>
        ParseDocument(text, specDirectory, DashSpecParseOptions.Default);

    public static DashboardDocument ParseDocument(
        string text,
        string? specDirectory,
        DashSpecParseOptions parseOptions) =>
        DashboardComposer.Parse(text, specDirectory, parseOptions);

    public static string? ReadRuntimeManifest(string text)
    {
        if (DocumentParseBridge.ReadRuntimePath is { } read)
        {
            return read(text);
        }

        throw new InvalidOperationException(
            "Document parse bridge not registered. Reference DashSpec.Execution.Core or register DocumentParseBridge.ReadRuntimePath.");
    }

    public static string? ReadConfigurationValue(string text, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return key.ToLowerInvariant() switch
        {
            "diagramlibrary" => DocumentParseBridge.ReadDiagramLibraryPath?.Invoke(text)
                ?? throw BridgeNotRegistered(nameof(DocumentParseBridge.ReadDiagramLibraryPath)),
            "palette" => DocumentParseBridge.ReadPalettePath?.Invoke(text)
                ?? throw BridgeNotRegistered(nameof(DocumentParseBridge.ReadPalettePath)),
            "sqldialect" => throw new InvalidOperationException(
                "Use DocumentParseBridge.ReadSqlDialect for sqldialect configuration."),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown configuration key."),
        };
    }

    private static InvalidOperationException BridgeNotRegistered(string member) =>
        new($"Document parse bridge not registered. Reference DashSpec.Execution.Core or register {member}.");
}
