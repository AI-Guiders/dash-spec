using DashSpec.Core.Model;

namespace DashSpec.Core.Parsing;

/// <summary>Public entry point for parsing .dashspec files.</summary>
public static class DashSpecParser
{
    public static string? ReadRuntimePath(string text) => DashboardParser.ReadRuntimePath(text);

    [Obsolete("Use ReadRuntimePath. @config is a deprecated alias for @runtime.")]
    public static string? ReadConfigPath(string text) => DashboardParser.ReadConfigPath(text);

    public static string? ReadDiagramLibraryPath(string text) => DashboardParser.ReadDiagramLibraryPath(text);

    public static string? ReadPalettePath(string text) => DashboardParser.ReadPalettePath(text);

    public static SqlDialect ReadSqlDialect(string text) => DashboardParser.ReadSqlDialect(text);

    public static (string Id, string Title) ReadDashboardHeader(string text) => DashboardParser.ReadDashboardHeader(text);

    public static DashboardDocument Parse(string text, string? specDirectory = null) =>
        Parse(text, specDirectory, DashSpecParseOptions.Default);

    public static DashboardDocument Parse(
        string text,
        string? specDirectory,
        DashSpecParseOptions parseOptions) =>
        DashboardComposer.Parse(text, specDirectory, parseOptions);

    internal static IReadOnlyList<Token> Tokenize(string text) => DashSpecLexer.Tokenize(text);
}
