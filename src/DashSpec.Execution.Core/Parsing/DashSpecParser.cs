using DashSpec.Core.Model;
using DashSpec.Core.Parsing;

namespace DashSpec.Execution.Parsing;

/// <summary>
/// Stable parse facade for Host, Studio, and tests (ADR-0048 §6).
/// Transitional: forwards to C# parsers in <see cref="Core.Parsing.DashSpecParser"/> until F# Modeling.Parse ships.
/// </summary>
public static class DashSpecParser
{
    static DashSpecParser() => LayoutParseRegistration.EnsureRegistered();

    public static string? ReadRuntimePath(string text) => Core.Parsing.DashSpecParser.ReadRuntimePath(text);

    [Obsolete("Use ReadRuntimePath. @config is a deprecated alias for @runtime.")]
    public static string? ReadConfigPath(string text) => Core.Parsing.DashSpecParser.ReadConfigPath(text);

    public static string? ReadDiagramLibraryPath(string text) => Core.Parsing.DashSpecParser.ReadDiagramLibraryPath(text);

    public static string? ReadPalettePath(string text) => Core.Parsing.DashSpecParser.ReadPalettePath(text);

    public static SqlDialect ReadSqlDialect(string text) => Core.Parsing.DashSpecParser.ReadSqlDialect(text);

    public static (string Id, string Title) ReadDashboardHeader(string text) =>
        Core.Parsing.DashSpecParser.ReadDashboardHeader(text);

    public static DashboardDocument Parse(string text, string? specDirectory = null) =>
        Core.Parsing.DashSpecParser.Parse(text, specDirectory);

    public static DashboardDocument Parse(
        string text,
        string? specDirectory,
        DashSpecParseOptions parseOptions) =>
        Core.Parsing.DashSpecParser.Parse(text, specDirectory, parseOptions);
}
