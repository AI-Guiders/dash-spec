using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

internal static class DashboardParser
{
    public static string? ReadRuntimePath(string text) =>
        DocumentParseBridge.ReadRuntimePath?.Invoke(text)
        ?? throw BridgeNotRegistered(nameof(DocumentParseBridge.ReadRuntimePath));

    [Obsolete("Use ReadRuntimePath. @config is a deprecated alias for @runtime.")]
    public static string? ReadConfigPath(string text) =>
        DocumentParseBridge.ReadConfigPath?.Invoke(text)
        ?? throw BridgeNotRegistered(nameof(DocumentParseBridge.ReadConfigPath));

    public static string? ReadDiagramLibraryPath(string text) =>
        DocumentParseBridge.ReadDiagramLibraryPath?.Invoke(text)
        ?? throw BridgeNotRegistered(nameof(DocumentParseBridge.ReadDiagramLibraryPath));

    public static string? ReadPalettePath(string text) =>
        DocumentParseBridge.ReadPalettePath?.Invoke(text)
        ?? throw BridgeNotRegistered(nameof(DocumentParseBridge.ReadPalettePath));

    public static SqlDialect ReadSqlDialect(string text) =>
        DocumentParseBridge.ReadSqlDialect?.Invoke(text)
        ?? throw BridgeNotRegistered(nameof(DocumentParseBridge.ReadSqlDialect));

    public static (string Id, string Title) ReadDashboardHeader(string text) =>
        DocumentParseBridge.ReadDashboardHeader?.Invoke(text)
        ?? throw BridgeNotRegistered(nameof(DocumentParseBridge.ReadDashboardHeader));

    internal static string ReadPaletteReference(TokenReader reader)
    {
        if (reader.RawKind is TokenKind.Eq)
        {
            reader.Advance();
        }

        return reader.ReadScalarValue();
    }

    private static InvalidOperationException BridgeNotRegistered(string member) =>
        new($"Document parse bridge not registered. Reference DashSpec.Execution.Core or register {member}.");
}
